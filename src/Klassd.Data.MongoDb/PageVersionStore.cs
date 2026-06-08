using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Page draft + published-history store over the <c>pageVersions</c> collection. One draft per page
/// is enforced by a partial unique index (see <see cref="MongoIndexInitializer"/>).
/// </summary>
public sealed class PageVersionStore(MongoContext context) : IPageVersionStore
{
    private IMongoCollection<PageVersionRecord> Col => context.PageVersions;
    private static FilterDefinitionBuilder<PageVersionRecord> F => Builders<PageVersionRecord>.Filter;

    private static FilterDefinition<PageVersionRecord> Draft(string pageId) =>
        F.And(F.Eq(x => x.PageId, pageId), F.Eq(x => x.Status, PageVersionStatus.Draft));

    public async Task<PageVersionRecord?> GetDraftAsync(string pageId, CancellationToken ct = default) =>
        await Col.Find(Draft(pageId)).FirstOrDefaultAsync(ct);

    public async Task SaveDraftAsync(PageVersionRecord draft, CancellationToken ct = default)
    {
        await Col.DeleteManyAsync(Draft(draft.PageId), ct);
        await Col.InsertOneAsync(draft, cancellationToken: ct);
    }

    public Task DeleteDraftAsync(string pageId, CancellationToken ct = default) =>
        Col.DeleteManyAsync(Draft(pageId), ct);

    public async Task<IReadOnlyList<PageVersionRecord>> GetHistoryAsync(string pageId, CancellationToken ct = default) =>
        await Col.Find(F.And(F.Eq(x => x.PageId, pageId), F.Ne(x => x.Status, PageVersionStatus.Draft)))
                 .SortByDescending(x => x.Number).ToListAsync(ct);

    public async Task<PageVersionRecord?> GetVersionAsync(string versionId, CancellationToken ct = default) =>
        await Col.Find(F.Eq(x => x.VersionId, versionId)).FirstOrDefaultAsync(ct);

    public async Task AppendPublishedAsync(PageVersionRecord version, int keepLast, CancellationToken ct = default)
    {
        await Col.InsertOneAsync(version, cancellationToken: ct);
        if (keepLast <= 0) return;

        // Keep the newest `keepLast` non-draft versions; delete the rest.
        var stale = await Col.Find(F.And(F.Eq(x => x.PageId, version.PageId), F.Ne(x => x.Status, PageVersionStatus.Draft)))
            .SortByDescending(x => x.Number)
            .Skip(keepLast)
            .Project(x => x.VersionId)
            .ToListAsync(ct);
        if (stale.Count > 0)
            await Col.DeleteManyAsync(F.In(x => x.VersionId, stale), ct);
    }

    public Task DeleteForPageAsync(string pageId, CancellationToken ct = default) =>
        Col.DeleteManyAsync(F.Eq(x => x.PageId, pageId), ct);
}
