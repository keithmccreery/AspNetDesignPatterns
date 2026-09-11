using System.Security.Claims;

using KAM.Common.Authorization.Requirements;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace KAM.Common.Tests.Authorization.Requirements;

[TestFixture]
public class ValidClientIdHandlerTests
{
    private static async Task<bool> SucceedsAsync(ClaimsPrincipal user, IEnumerable<string> allowedClientIds)
    {
        ValidClientIdRequirement requirement = new(allowedClientIds);
        ValidClientIdHandler handler = new(NullLogger<ValidClientIdHandler>.Instance);
        AuthorizationHandlerContext context = new([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal UserWithClientId(string? clientId)
    {
        List<Claim> claims = clientId is null ? [] : [new Claim("client_id", clientId)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Test]
    public async Task Succeeds_when_the_client_id_claim_is_in_the_allowed_list()
    {
        // Arrange & Act
        bool succeeded = await SucceedsAsync(UserWithClientId("web-client-01"), ["web-client-01", "mobile-client-02"]);

        // Assert
        succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Matches_case_insensitively()
    {
        // Arrange & Act
        bool succeeded = await SucceedsAsync(UserWithClientId("WEB-CLIENT-01"), ["web-client-01"]);

        // Assert
        succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Fails_when_the_client_id_is_not_in_the_allowed_list()
    {
        // Arrange & Act
        bool succeeded = await SucceedsAsync(UserWithClientId("unknown-client"), ["web-client-01"]);

        // Assert
        succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Fails_when_the_claim_is_missing()
    {
        // Arrange & Act
        bool succeeded = await SucceedsAsync(UserWithClientId(null), ["web-client-01"]);

        // Assert
        succeeded.Should().BeFalse();
    }

    [Test]
    public void Rejects_a_null_allowed_client_ids_argument()
    {
        // Arrange & Act
        Func<ValidClientIdRequirement> act = () => new ValidClientIdRequirement(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
