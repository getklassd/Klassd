using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>Runs the IMediaStore contract scenarios against the REAL SQLite adapter (temp db file).</summary>
public class SqliteMediaStoreTests
{
    [Test]
    public async Task Media_crud_round_trip()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await MediaAdapterScenarios.CrudRoundTrip(harness.Services);
    }

    [Test]
    public async Task Media_list_filters_by_section()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await MediaAdapterScenarios.ListFiltersBySection(harness.Services);
    }
}
