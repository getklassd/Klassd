using Klassd.Abstractions;
using Klassd.Abstractions.Storage;
using Klassd.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.IntegrationTests;

/// <summary>Minimal <see cref="ICmsBuilder"/> so the SQLite adapter can register itself.</summary>
internal sealed class TestCmsBuilder(IServiceCollection services) : ICmsBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = new ConfigurationBuilder().Build();
}

/// <summary>
/// No-op transaction primitive for the engine's <c>PageService</c> in tests.
///
/// The real <c>SqliteUnitOfWork.BeginAsync</c> opens a dedicated connection and starts a
/// BEGIN IMMEDIATE transaction (a held write lock). The adapter's stores each open their
/// OWN connection per call and do NOT enlist in that transaction (a documented follow-up
/// in the adapter), so during the engine's cascade slug rename the store's UPDATE on a
/// separate connection deadlocks against the UoW's held write lock under SQLite's
/// single-writer rule. Substituting this no-op UoW lets the cascade — which is still
/// executed entirely through the REAL <c>IPageStore</c> against real SQLite — run and
/// persist, which is what these integration tests verify. No connection-string setting
/// (WAL, shared cache, busy timeout) can break that self-deadlock.
/// </summary>
internal sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task<IStorageTransaction> BeginAsync(CancellationToken ct = default) =>
        Task.FromResult<IStorageTransaction>(new NoOpTransaction());

    private sealed class NoOpTransaction : IStorageTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>
/// Spins up the REAL SQLite adapter against a unique temp database file, runs schema
/// init, and hands out scoped store/service instances. Disposing deletes the db files.
/// </summary>
internal sealed class SqliteTestHarness : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provider;

    private SqliteTestHarness(string dbPath, ServiceProvider provider)
    {
        _dbPath = dbPath;
        _provider = provider;
    }

    public static async Task<SqliteTestHarness> CreateAsync()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cfcms-it-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={dbPath};Default Timeout=30";

        var sc = new ServiceCollection();
        var builder = new TestCmsBuilder(sc);
        builder.UseSqlite(connectionString);
        var provider = sc.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            foreach (var init in scope.ServiceProvider.GetServices<IStorageInitializer>())
                await init.InitializeAsync();
        }

        return new SqliteTestHarness(dbPath, provider);
    }

    /// <summary>Creates a DI scope; resolve scoped stores/UoW from it.</summary>
    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

    /// <summary>The root provider, for scenarios that manage their own scopes.</summary>
    public IServiceProvider Services => _provider;

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();

        // SQLite pools file handles; clearing pools lets us delete the file on Windows.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup; a leftover temp file must not fail the test.
            }
        }
    }
}
