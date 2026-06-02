using Klassd.Abstractions.Media;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Single-database media metadata store.</summary>
public sealed class MediaStore(MongoContext context) : IMediaStore
{
    private static readonly FilterDefinitionBuilder<MediaRecord> F = Builders<MediaRecord>.Filter;

    public async Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default) =>
        await context.Media
            .Find(F.Eq(x => x.Section, section))
            .SortByDescending(x => x.UploadedAt)
            .ToListAsync(ct);

    public async Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default) =>
        await context.Media
            .Find(F.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(ct);

    public Task InsertAsync(MediaRecord media, CancellationToken ct = default) =>
        context.Media.InsertOneAsync(media, cancellationToken: ct);

    public Task<MediaRecord?> UpdateAsync(MediaRecord media, CancellationToken ct = default)
    {
        var options = new FindOneAndReplaceOptions<MediaRecord> { ReturnDocument = ReturnDocument.After };
        return context.Media.FindOneAndReplaceAsync<MediaRecord>(
            F.Eq(x => x.Id, media.Id), media, options, ct)!;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var result = await context.Media.DeleteOneAsync(F.Eq(x => x.Id, id), ct);
        return result.DeletedCount > 0;
    }
}
