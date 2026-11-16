using System.Text.Json;
using System.Text.Json.Serialization;

using KAM.Common.Pagination;

namespace KAM.Common.Tests.Pagination;

[TestFixture]
public class CursorPageTests
{
    // Like the app's own JSON settings: nulls are omitted.
    private static readonly JsonSerializerOptions OmitNulls = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record IdCursor(int Id);

    private static int[] Rows(int count) => [.. Enumerable.Range(1, count)];

    [Test]
    public void Fewer_rows_than_limit_plus_one_is_the_last_page()
    {
        // Act
        CursorPage<int> page = CursorPage.From(Rows(3), limit: 3, keyOf: row => new IdCursor(row));

        // Assert
        page.Items.Should().Equal(1, 2, 3);
        page.NextCursor.Should().BeNull();
    }

    [Test]
    public void The_extra_row_is_dropped_and_the_last_kept_row_becomes_the_cursor()
    {
        // Act
        CursorPage<int> page = CursorPage.From(Rows(4), limit: 3, keyOf: row => new IdCursor(row));

        // Assert
        page.Items.Should().Equal(1, 2, 3);
        CursorCodec.TryDecode(page.NextCursor, out IdCursor? key).Should().BeTrue();
        key.Should().Be(new IdCursor(3));
    }

    [Test]
    public void A_limit_below_one_is_a_programming_error()
    {
        // Arrange
        Func<CursorPage<int>> act = () => CursorPage.From(Rows(1), limit: 0, keyOf: row => new IdCursor(row));

        // Act & Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Map_converts_the_items_and_keeps_the_cursor()
    {
        // Arrange
        CursorPage<int> page = new([1, 2], "next");

        // Act
        CursorPage<string> mapped = page.Map(i => $"#{i}");

        // Assert
        mapped.Items.Should().Equal("#1", "#2");
        mapped.NextCursor.Should().Be("next");
    }

    [Test]
    public void A_null_next_cursor_is_still_written_when_nulls_are_otherwise_omitted()
    {
        // Arrange
        CursorPage<int> lastPage = new([1], NextCursor: null);

        // Act
        string json = JsonSerializer.Serialize(lastPage, OmitNulls);

        // Assert
        json.Should().Be("""{"items":[1],"nextCursor":null}""");
    }

    [Test]
    public void EffectiveLimit_uses_the_request_limit_or_the_default()
    {
        // Arrange
        ICursorPagedRequest withLimit = new PagedRequest(Limit: 5, Cursor: null);
        ICursorPagedRequest withoutLimit = new PagedRequest(Limit: null, Cursor: null);

        // Act & Assert
        withLimit.EffectiveLimit(defaultLimit: 20).Should().Be(5);
        withoutLimit.EffectiveLimit(defaultLimit: 20).Should().Be(20);
    }

    private sealed record PagedRequest(int? Limit, string? Cursor) : ICursorPagedRequest;
}
