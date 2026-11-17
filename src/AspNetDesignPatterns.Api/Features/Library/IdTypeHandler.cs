using System.Data;
using System.Globalization;

using Dapper;

namespace AspNetDesignPatterns.Api.Features.Library;

/// <summary>
/// Lets Dapper read and write a strongly typed id (<see cref="BookId"/>, <see cref="LoanId"/>)
/// as the plain <c>INTEGER</c> column it is: the database is "the wire" too. With one registered
/// per id type (by <see cref="LibraryDependencies"/>), row properties and SQL parameters can be
/// the typed id, and the repository never unwraps one by hand.
/// </summary>
internal sealed class IdTypeHandler<TId> : SqlMapper.TypeHandler<TId>
    where TId : struct
{
    private readonly Func<int, TId> _create;
    private readonly Func<TId, int> _value;

    public IdTypeHandler(Func<int, TId> create, Func<TId, int> value)
    {
        _create = create;
        _value = value;
    }

    public override void SetValue(IDbDataParameter parameter, TId value)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        parameter.DbType = DbType.Int32;
        parameter.Value = _value(value);
    }

    // SQLite hands back every INTEGER as a long.
    public override TId Parse(object value) => _create(Convert.ToInt32(value, CultureInfo.InvariantCulture));
}
