using System.Data.Common;

using KAM.Common.Data;
using KAM.Common.Tests.TestSupport;

namespace KAM.Common.Tests.Data;

/// <summary>
/// <see cref="UnitOfWork"/> against a real in-memory SQLite database: whether rows survive is
/// read on a separate connection, the way another request would see them.
/// </summary>
[TestFixture]
public class UnitOfWorkTests
{
    private static readonly Error Rejected = Error.Conflict("Test.Rejected", "rejected");

    private InMemorySqliteDatabase _database = null!;
    private UnitOfWork _unitOfWork = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new InMemorySqliteDatabase();
        _database.Execute("CREATE TABLE items (value INTEGER NOT NULL);");
        _unitOfWork = new UnitOfWork(_database);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        _database.Dispose();
    }

    private int RowCount => _database.Scalar<int>("SELECT COUNT(*) FROM items;");

    private async Task InsertAsync(int value, CancellationToken cancellationToken)
    {
        DbConnection connection = await _unitOfWork.GetConnectionAsync(cancellationToken);
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = _unitOfWork.Transaction;
        command.CommandText = $"INSERT INTO items (value) VALUES ({value});";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [Test]
    public async Task Commits_when_the_work_succeeds()
    {
        // Act
        Result<int> result = await _unitOfWork.ExecuteInTransactionAsync<int>(
            async token =>
            {
                await InsertAsync(1, token);
                await InsertAsync(2, token);
                return 2;
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        RowCount.Should().Be(2);
    }

    [Test]
    public async Task Rolls_back_everything_when_the_work_returns_a_failure()
    {
        // Act — the first insert has already happened when the failure is returned
        Result<int> result = await _unitOfWork.ExecuteInTransactionAsync<int>(
            async token =>
            {
                await InsertAsync(1, token);
                return Rejected;
            },
            CancellationToken.None);

        // Assert
        result.Error.Should().Be(Rejected);
        RowCount.Should().Be(0);
    }

    [Test]
    public async Task Rolls_back_and_rethrows_when_the_work_throws()
    {
        // Arrange
        Func<Task> act = () => _unitOfWork.ExecuteInTransactionAsync<int>(
            async token =>
            {
                await InsertAsync(1, token);
                throw new InvalidOperationException("boom");
            },
            CancellationToken.None);

        // Act & Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        RowCount.Should().Be(0);
        _unitOfWork.Transaction.Should().BeNull();
    }

    [Test]
    public async Task The_non_generic_overload_commits_and_rolls_back_the_same_way()
    {
        // Act
        Result committed = await _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await InsertAsync(1, token);
                return Result.Success();
            },
            CancellationToken.None);

        Result rolledBack = await _unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await InsertAsync(2, token);
                return Rejected;
            },
            CancellationToken.None);

        // Assert
        committed.IsSuccess.Should().BeTrue();
        rolledBack.IsFailure.Should().BeTrue();
        RowCount.Should().Be(1);
    }

    [Test]
    public async Task A_nested_call_joins_the_outer_transaction_so_the_outer_failure_undoes_it()
    {
        // Arrange
        DbTransaction? outer = null;
        DbTransaction? inner = null;

        // Act
        Result<int> result = await _unitOfWork.ExecuteInTransactionAsync<int>(
            async token =>
            {
                outer = _unitOfWork.Transaction;
                Result<int> nested = await _unitOfWork.ExecuteInTransactionAsync<int>(
                    async innerToken =>
                    {
                        inner = _unitOfWork.Transaction;
                        await InsertAsync(1, innerToken);
                        return 1;
                    },
                    token);

                return nested.IsSuccess ? Rejected : nested.Error;
            },
            CancellationToken.None);

        // Assert — the nested success did not commit on its own
        result.Error.Should().Be(Rejected);
        inner.Should().BeSameAs(outer);
        RowCount.Should().Be(0);
    }

    [Test]
    public async Task Transaction_is_set_only_inside_the_work()
    {
        // Arrange
        DbTransaction? inside = null;

        // Act
        DbTransaction? before = _unitOfWork.Transaction;
        await _unitOfWork.ExecuteInTransactionAsync(
            _ =>
            {
                inside = _unitOfWork.Transaction;
                return Task.FromResult(Result.Success());
            },
            CancellationToken.None);

        // Assert
        before.Should().BeNull();
        inside.Should().NotBeNull();
        _unitOfWork.Transaction.Should().BeNull();
    }

    [Test]
    public async Task Opens_one_connection_lazily_and_closes_it_on_dispose()
    {
        // Arrange
        int openedBeforeUse = _database.ConnectionsOpened;

        // Act
        DbConnection first = await _unitOfWork.GetConnectionAsync(CancellationToken.None);
        DbConnection second = await _unitOfWork.GetConnectionAsync(CancellationToken.None);
        await _unitOfWork.DisposeAsync();

        // Assert
        openedBeforeUse.Should().Be(0);
        second.Should().BeSameAs(first);
        _database.ConnectionsOpened.Should().Be(1);
        first.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Test]
    public async Task A_synchronously_disposed_scope_disposes_the_unit_of_work_too()
    {
        // Arrange — a plain `using` scope, as a background job would create
        ServiceCollection services = new();
        services.AddSingleton<IDbConnectionFactory>(_database).AddUnitOfWork();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IServiceScope scope = provider.CreateScope();
        DbConnection connection = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .GetConnectionAsync(CancellationToken.None);

        // Act
        Action dispose = scope.Dispose;

        // Assert
        dispose.Should().NotThrow();
        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Test]
    public void AddUnitOfWork_registers_one_scoped_unit_of_work_however_often_it_is_called()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddUnitOfWork().AddUnitOfWork();

        // Assert
        services.Should().ContainSingle(d => d.ServiceType == typeof(IUnitOfWork))
            .Which.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }
}
