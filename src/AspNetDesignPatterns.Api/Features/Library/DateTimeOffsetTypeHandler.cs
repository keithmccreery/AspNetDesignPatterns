using System.Data;
using System.Globalization;

using Dapper;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// SQLite has no date type; timestamps are stored as ISO-8601 text in UTC
/// (<c>2026-11-15T10:30:00.0000000+00:00</c>). Fixed-width UTC text also sorts correctly as text.
/// Dapper can't turn that text back into a <see cref="DateTimeOffset"/> on its own, hence this
/// handler (registered once by <see cref="LibraryDependencies"/>).
/// </summary>
internal sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        parameter.DbType = DbType.String;
        parameter.Value = value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    public override DateTimeOffset Parse(object value) =>
        DateTimeOffset.Parse((string) value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
