using System.Reflection;

using KAM.Common.Data;
using KAM.Common.Tests.TestSupport;

using Microsoft.Data.Sqlite;

namespace KAM.Common.Tests.Data;

/// <summary>
/// <see cref="SqlScriptMigrator"/> against the scripts embedded from this folder's
/// <c>Scripts/Good</c> and <c>Scripts/Broken</c>, on a real in-memory SQLite database.
/// </summary>
[TestFixture]
public class SqlScriptMigratorTests
{
    private const string GOOD = "KAM.Common.Tests.Data.Scripts.Good.";
    private const string BROKEN = "KAM.Common.Tests.Data.Scripts.Broken.";

    private static readonly Assembly ScriptAssembly = typeof(SqlScriptMigratorTests).Assembly;

    private InMemorySqliteDatabase _database = null!;

    [SetUp]
    public void SetUp() => _database = new InMemorySqliteDatabase();

    [TearDown]
    public void TearDown() => _database.Dispose();

    private async Task<IReadOnlyList<string>> ApplyAsync(string prefix)
    {
        await using SqliteConnection connection = new(_database.ConnectionString);
        await connection.OpenAsync();
        return await SqlScriptMigrator.ApplyAsync(connection, ScriptAssembly, prefix, TimeProvider.System, CancellationToken.None);
    }

    [Test]
    public async Task Applies_every_sql_script_in_order_and_records_it()
    {
        // Act
        IReadOnlyList<string> applied = await ApplyAsync(GOOD);

        // Assert
        applied.Should().Equal("001_create.sql", "002_seed.sql");
        _database.Scalar<int>("SELECT COUNT(*) FROM things;").Should().Be(2);
        _database.Scalar<int>("SELECT COUNT(*) FROM schema_versions;").Should().Be(2);
    }

    [Test]
    public async Task Running_again_applies_nothing()
    {
        // Arrange
        await ApplyAsync(GOOD);

        // Act
        IReadOnlyList<string> secondRun = await ApplyAsync(GOOD);

        // Assert
        secondRun.Should().BeEmpty();
        _database.Scalar<int>("SELECT COUNT(*) FROM things;").Should().Be(2);
    }

    [Test]
    public async Task A_failing_script_is_rolled_back_whole_and_not_recorded()
    {
        // Arrange
        Func<Task> act = () => ApplyAsync(BROKEN);

        // Act & Assert
        await act.Should().ThrowAsync<SqliteException>();
        _database.Scalar<string>("SELECT group_concat(script) FROM schema_versions;").Should().Be("001_create.sql");
        _database.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'half_done';").Should().Be(0);
    }
}
