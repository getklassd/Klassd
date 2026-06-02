using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Single-database user store.</summary>
public sealed class UserStore(MongoContext context) : IUserStore
{
    private static readonly FilterDefinitionBuilder<UserRecord> F = Builders<UserRecord>.Filter;

    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        await context.Users
            .Find(F.Eq(x => x.Username, username))
            .FirstOrDefaultAsync(ct);

    public async Task<UserRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        await context.Users
            .Find(F.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken ct = default) =>
        await context.Users.Find(F.Empty).ToListAsync(ct);

    public Task InsertAsync(UserRecord user, CancellationToken ct = default) =>
        context.Users.InsertOneAsync(user, cancellationToken: ct);

    public async Task<UserRecord?> FindByExternalAsync(string provider, string externalId, CancellationToken ct = default) =>
        await context.Users
            .Find(F.Eq(x => x.Provider, provider) & F.Eq(x => x.ExternalId, externalId))
            .FirstOrDefaultAsync(ct);

    public async Task<UserRecord?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        await context.Users
            .Find(F.Eq(x => x.Email, email))
            .FirstOrDefaultAsync(ct);

    public Task UpdateAsync(UserRecord user, CancellationToken ct = default) =>
        context.Users.ReplaceOneAsync(F.Eq(x => x.Id, user.Id), user, cancellationToken: ct);
}
