using System.Net;
using System.Net.Http.Json;

using AspNetDesignPatterns.Api.Features.Library;

using KAM.Common.Pagination;

using Microsoft.AspNetCore.Mvc;

namespace AspNetDesignPatterns.Api.Tests.Integration;

/// <summary>
/// The Library slice through the real host: routing, auth, binding (including
/// <see cref="MemberId"/> from the token), validation, the database the app migrated and seeded at
/// startup, and the HTTP shapes. Tests share that database, so each uses its own member and puts
/// back any copy it borrows.
/// </summary>
[TestFixture]
public class LibraryEndpointTests
{
    private const string BOOKS = "/api/v1/library/books";
    private const string LOANS = "/api/v1/library/loans";

    private static ApiFactory Factory => GlobalTestSetup.Factory;

    private static Task<HttpClient> NewMemberAsync() =>
        Factory.CreateAuthenticatedClientAsync(subject: $"member-{Guid.NewGuid():N}");

    private static async Task<BookResponse> FindBookAsync(HttpClient client, string title)
    {
        CursorPage<BookResponse>? page = await client.GetFromJsonAsync<CursorPage<BookResponse>>($"{BOOKS}?search={Uri.EscapeDataString(title)}");
        return page!.Items.Single(b => b.Title == title);
    }

    [Test]
    public async Task Returns_401_without_a_token()
    {
        // Arrange & Act
        HttpResponseMessage response = await Factory.CreateClient().GetAsync(BOOKS);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Following_nextCursor_pages_through_the_whole_seeded_catalogue_exactly_once()
    {
        // Arrange
        HttpClient client = await NewMemberAsync();
        List<int> seen = [];
        string? cursor = null;
        int pages = 0;

        // Act
        do
        {
            string url = cursor is null ? $"{BOOKS}?limit=7" : $"{BOOKS}?limit=7&cursor={cursor}";
            CursorPage<BookResponse>? page = await client.GetFromJsonAsync<CursorPage<BookResponse>>(url);
            seen.AddRange(page!.Items.Select(b => b.Id));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null);

        // Assert — 50 seeded books, 7 per page
        seen.Should().HaveCount(50).And.OnlyHaveUniqueItems();
        pages.Should().Be(8);
    }

    [Test]
    public async Task The_last_page_says_so_with_an_explicit_null_nextCursor()
    {
        // Arrange
        HttpClient client = await NewMemberAsync();

        // Act
        string json = await client.GetStringAsync($"{BOOKS}?search=Wuthering");

        // Assert
        json.Should().Contain("\"nextCursor\":null");
    }

    [Test]
    public async Task A_tampered_cursor_is_a_400_validation_problem()
    {
        // Arrange
        HttpClient client = await NewMemberAsync();

        // Act
        HttpResponseMessage response = await client.GetAsync($"{BOOKS}?cursor=tampered");
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem!.Errors.Should().ContainKey("Cursor");
    }

    [Test]
    public async Task A_search_too_short_for_the_index_is_a_400_validation_problem()
    {
        // Arrange
        HttpClient client = await NewMemberAsync();

        // Act
        HttpResponseMessage response = await client.GetAsync($"{BOOKS}?search=em");
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem!.Errors.Should().ContainKey("Search");
    }

    [Test]
    public async Task Borrow_returns_201_with_a_Location_that_serves_the_loan_then_return_closes_it()
    {
        // Arrange
        HttpClient client = await NewMemberAsync();
        BookResponse book = await FindBookAsync(client, "Pride and Prejudice");

        // Act
        HttpResponseMessage borrowed = await client.PostAsync($"{BOOKS}/{book.Id}/loans", content: null);
        LoanResponse? loan = await borrowed.Content.ReadFromJsonAsync<LoanResponse>();
        LoanResponse? fetched = await client.GetFromJsonAsync<LoanResponse>(borrowed.Headers.Location);
        HttpResponseMessage returned = await client.PostAsync($"{LOANS}/{loan!.Id}/return", content: null);
        LoanResponse? closed = await returned.Content.ReadFromJsonAsync<LoanResponse>();

        // Assert
        borrowed.StatusCode.Should().Be(HttpStatusCode.Created);
        borrowed.Headers.Location!.AbsolutePath.Should().Be($"{LOANS}/{loan.Id}");
        fetched.Should().BeEquivalentTo(loan);
        returned.StatusCode.Should().Be(HttpStatusCode.OK);
        closed!.ReturnedAt.Should().NotBeNull();
        (await FindBookAsync(client, "Pride and Prejudice")).AvailableCopies.Should().Be(book.AvailableCopies);
    }

    [Test]
    public async Task Borrowing_a_book_with_no_copy_left_is_a_409_problem_with_the_error_code()
    {
        // Arrange — Beowulf has one copy
        HttpClient holder = await NewMemberAsync();
        HttpClient other = await NewMemberAsync();
        BookResponse book = await FindBookAsync(holder, "Beowulf");
        LoanResponse? loan = await (await holder.PostAsync($"{BOOKS}/{book.Id}/loans", content: null))
            .Content.ReadFromJsonAsync<LoanResponse>();

        try
        {
            // Act
            HttpResponseMessage response = await other.PostAsync($"{BOOKS}/{book.Id}/loans", content: null);
            ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            problem!.Extensions["errorCode"]!.ToString().Should().Be(LibraryErrors.NoCopiesAvailable.Code);
        }
        finally
        {
            await holder.PostAsync($"{LOANS}/{loan!.Id}/return", content: null);
        }
    }

    [Test]
    public async Task A_members_loan_list_and_loan_ids_are_private_to_them()
    {
        // Arrange
        HttpClient owner = await NewMemberAsync();
        HttpClient other = await NewMemberAsync();
        BookResponse book = await FindBookAsync(owner, "Dracula");
        LoanResponse? loan = await (await owner.PostAsync($"{BOOKS}/{book.Id}/loans", content: null))
            .Content.ReadFromJsonAsync<LoanResponse>();

        try
        {
            // Act
            CursorPage<LoanResponse>? ownerLoans = await owner.GetFromJsonAsync<CursorPage<LoanResponse>>(LOANS);
            CursorPage<LoanResponse>? otherLoans = await other.GetFromJsonAsync<CursorPage<LoanResponse>>(LOANS);
            HttpResponseMessage otherFetch = await other.GetAsync($"{LOANS}/{loan!.Id}");

            // Assert
            ownerLoans!.Items.Should().ContainSingle().Which.Id.Should().Be(loan.Id);
            otherLoans!.Items.Should().BeEmpty();
            otherFetch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await owner.PostAsync($"{LOANS}/{loan!.Id}/return", content: null);
        }
    }
}
