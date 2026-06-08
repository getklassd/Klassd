using Klassd.Abstractions.Storage;
using Klassd.Data.Postgres;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// Runs the storage-adapter contract scenarios against the REAL PostgreSQL adapter on a
/// Testcontainers Postgres instance. Requires Docker — auto-skipped when unavailable.
/// </summary>
[SkipWhenDockerUnavailable]
public class PostgresAdapterTests
{
    private static PostgreSqlContainer? _container;
    private static ServiceProvider? _provider;

    [Before(HookType.Class)]
    public static async Task StartAsync()
    {
        if (!DockerProbe.IsAvailable()) return;

        _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _container.StartAsync();

        var services = new ServiceCollection();
        new TestCmsBuilder(services).UsePostgres(_container.GetConnectionString());
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        foreach (var init in scope.ServiceProvider.GetServices<IStorageInitializer>())
            await init.InitializeAsync();
    }

    [After(HookType.Class)]
    public static async Task StopAsync()
    {
        if (_provider is not null) await _provider.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    [Test] public Task Crud_round_trip() => PageAdapterScenarios.CrudRoundTrip(_provider!);
    [Test] public Task Queries_and_children() => PageAdapterScenarios.QueriesAndChildren(_provider!);
    [Test] public Task Duplicate_slug_throws() => PageAdapterScenarios.DuplicateSlugThrows(_provider!);
    [Test] public Task Cascade_rename_persists() => PageAdapterScenarios.CascadeRenamePersists(_provider!);
    [Test] public Task Translation_grouping() => PageAdapterScenarios.TranslationGrouping(_provider!);
    [Test] public Task Users_and_preferences() => PageAdapterScenarios.UsersAndPreferences(_provider!);

    [Test] public Task Media_crud_round_trip() => MediaAdapterScenarios.CrudRoundTrip(_provider!);
    [Test] public Task Media_list_filters_by_section() => MediaAdapterScenarios.ListFiltersBySection(_provider!);

    [Test] public Task Dictionary_crud_round_trip() => DictionaryAdapterScenarios.CrudRoundTrip(_provider!);
    [Test] public Task Settings_crud_round_trip() => SettingsAdapterScenarios.CrudRoundTrip(_provider!);
}
