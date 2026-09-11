using System.Text;

using KAM.Common.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace KAM.Common.Tests.Authentication;

[TestFixture]
public class DevTokenIssuerTests
{
    private const string SIGNING_KEY = "dev-token-issuer-tests-signing-key-01234567890";

    private static readonly JwtOptions Options = new()
    {
        SigningKey = SIGNING_KEY,
        Issuer = "https://tests/issuer",
        Audience = "tests-audience",
        AccessTokenMinutes = 30,
    };

    private static DevTokenIssuer CreateIssuer(TimeProvider? time = null) =>
        new(Microsoft.Extensions.Options.Options.Create(Options), time ?? TimeProvider.System);

    [Test]
    public async Task Issues_a_token_that_validates_against_the_configured_parameters()
    {
        // Arrange
        TokenResponse response = CreateIssuer().Issue("alice");

        TokenValidationParameters parameters = new()
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SIGNING_KEY)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
        };

        // Act
        TokenValidationResult validation = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, parameters);

        // Assert
        using (new AssertionScope())
        {
            response.TokenType.Should().Be("Bearer");
            validation.IsValid.Should().BeTrue();
            validation.Claims.Should().ContainKey("sub").WhoseValue.Should().Be("alice");
            validation.Claims.Should().ContainKey("scope").WhoseValue.Should().Be("weather:read");
        }
    }

    [Test]
    public void Expiry_is_now_plus_the_configured_lifetime()
    {
        // Arrange
        DateTimeOffset fixedNow = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DevTokenIssuer issuer = CreateIssuer(new FixedTimeProvider(fixedNow));

        // Act
        TokenResponse response = issuer.Issue("bob");

        // Assert
        response.ExpiresAtUtc.Should().Be(fixedNow.AddMinutes(30));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
