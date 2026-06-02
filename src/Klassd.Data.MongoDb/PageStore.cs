using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Single-database page store.</summary>
public sealed class PageStore(MongoContext context) : IPageStore
{
    private static readonly FilterDefinitionBuilder<PageRecord> F = Builders<PageRecord>.Filter;

    public async Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default) =>
        await context.Pages
            .Find(F.Eq(x => x.LocaleCode, localeCode))
            .ToListAsync(ct);

    public async Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        await context.Pages
            .Find(F.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default) =>
        await context.Pages
            .Find(F.Eq(x => x.ContentId, contentId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default) =>
        await context.Pages
            .Find(F.Eq(x => x.ParentId, parentId) & F.Eq(x => x.LocaleCode, localeCode))
            .ToListAsync(ct);

    public async Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default)
    {
        var filter = F.Eq(x => x.LocaleCode, localeCode) & F.Eq(x => x.Slug, slug);
        if (excludeId is not null)
            filter &= F.Ne(x => x.Id, excludeId);

        return await context.Pages.Find(filter).FirstOrDefaultAsync(ct);
    }

    public Task InsertAsync(PageRecord page, CancellationToken ct = default) =>
        context.Pages.InsertOneAsync(page, cancellationToken: ct);

    public Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default)
    {
        var options = new FindOneAndReplaceOptions<PageRecord> { ReturnDocument = ReturnDocument.After };
        return context.Pages.FindOneAndReplaceAsync<PageRecord>(
            F.Eq(x => x.Id, page.Id), page, options, ct)!;
    }

    public Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default)
    {
        var update = Builders<PageRecord>.Update
            .Set(x => x.Slug, slug)
            .Set(x => x.UpdatedAt, updatedAt);
        return context.Pages.UpdateOneAsync(F.Eq(x => x.Id, id), update, cancellationToken: ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var result = await context.Pages.DeleteOneAsync(F.Eq(x => x.Id, id), ct);
        return result.DeletedCount > 0;
    }
}
