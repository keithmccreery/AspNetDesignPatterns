using AspNetDesignPatterns.Api.DependencyInjection;

namespace AspNetDesignPatterns.Api.Shared.Auth;

/// <summary>
/// Issues a signed JWT for local testing so you can call secured endpoints from Scalar or
/// curl without standing up an identity provider. Mapped only in the Development environment.
/// The token itself is built by <see cref="DevTokenIssuer"/>.
/// </summary>
internal sealed class DevTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        IHostEnvironment environment = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        app.MapPost("/auth/token", (TokenRequest request, DevTokenIssuer issuer) =>
                TypedResults.Ok(issuer.Issue(request.Subject)))
            .WithName("CreateDevToken")
            .WithTags("Auth")
            .WithSummary("Issue a development JWT (Development environment only).")
            .AllowAnonymous()
            .Produces<TokenResponse>();
    }
}
