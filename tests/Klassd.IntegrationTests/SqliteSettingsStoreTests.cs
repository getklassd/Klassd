using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>Runs the ISettingsStore contract scenario against the REAL SQLite adapter (temp db file).</summary>
public class SqliteSettingsStoreTests
{
    [Test]
    public async Task Settings_crud_round_trip()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await SettingsAdapterScenarios.CrudRoundTrip(harness.Services);
    }
}
