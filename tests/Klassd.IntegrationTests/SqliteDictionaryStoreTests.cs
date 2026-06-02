using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>Runs the IDictionaryStore contract scenario against the REAL SQLite adapter (temp db file).</summary>
public class SqliteDictionaryStoreTests
{
    [Test]
    public async Task Dictionary_crud_round_trip()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await DictionaryAdapterScenarios.CrudRoundTrip(harness.Services);
    }
}
