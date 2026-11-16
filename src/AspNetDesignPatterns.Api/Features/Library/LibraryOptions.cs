using FluentValidation;

using KAM.Common.DependencyInjection;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>Library feature settings, bound from the "Library" section of <c>appsettings.json</c>.</summary>
public sealed class LibraryOptions : SettingsBase<LibraryOptions>, ISettingsSection
{
    public static string Section => "Library";

    /// <summary>
    /// SQLite connection string. A relative <c>Data Source</c> is resolved against the content
    /// root (see <see cref="SqliteConnectionFactory"/>), not the process's working directory.
    /// Not a secret for SQLite — a hosted database's connection string would come from
    /// <c>.env</c> like <c>Jwt__SigningKey</c>.
    /// </summary>
    public string ConnectionString { get; init; } = "Data Source=library.db;Foreign Keys=True;Default Timeout=5";

    /// <summary>Page size when a list request doesn't specify <c>limit</c>.</summary>
    public int DefaultPageSize { get; init; } = 20;

    /// <summary>The largest <c>limit</c> a list request may ask for.</summary>
    public int MaxPageSize { get; init; } = 100;

    /// <summary>How long a loan lasts before it's due back.</summary>
    public TimeSpan LoanPeriod { get; init; } = TimeSpan.FromDays(14);
}

internal sealed class LibraryOptionsValidator : AbstractValidator<LibraryOptions>
{
    public LibraryOptionsValidator()
    {
        RuleFor(x => x.ConnectionString).NotEmpty();
        RuleFor(x => x.MaxPageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.DefaultPageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(x => x.MaxPageSize);
        RuleFor(x => x.LoanPeriod)
            .GreaterThan(TimeSpan.Zero)
            .LessThanOrEqualTo(TimeSpan.FromDays(365));
    }
}
