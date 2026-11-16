using System.Data.Common;

namespace KAM.Common.Data;

/// <summary>
/// Opens a connection to the app's database. The one provider-specific seam: the app registers
/// an implementation for its database (SQLite, PostgreSQL, SQL Server, …) and everything in this
/// folder works against the <see cref="DbConnection"/> it returns.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates and opens a new connection. The caller owns (and disposes) it.</summary>
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
