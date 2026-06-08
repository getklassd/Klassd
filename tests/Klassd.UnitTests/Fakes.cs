using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;

namespace Klassd.UnitTests;

/// <summary>In-memory IPageStore faithful enough to exercise slug uniqueness + cascade renames.</summary>
public sealed class InMemoryPageStore : IPageStore
{
    private readonly List<PageRecord> _pages = new();

    public IReadOnlyList<PageRecord> Pages => _pages;

    public Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageRecord>>(_pages.Where(p => p.LocaleCode == localeCode).ToList());

    public Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageRecord>>(_pages.Where(p => p.ContentId == contentId).ToList());

    public Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageRecord>>(
            _pages.Where(p => p.ParentId == parentId && p.LocaleCode == localeCode).ToList());

    public Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p =>
            p.LocaleCode == localeCode && p.Slug == slug && p.Id != excludeId));

    public Task InsertAsync(PageRecord page, CancellationToken ct = default)
    {
        _pages.Add(page);
        return Task.CompletedTask;
    }

    public Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default)
    {
        var idx = _pages.FindIndex(p => p.Id == page.Id);
        if (idx < 0) return Task.FromResult<PageRecord?>(null);
        _pages[idx] = page;
        return Task.FromResult<PageRecord?>(page);
    }

    public Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default)
    {
        var existing = _pages.FirstOrDefault(p => p.Id == id);
        if (existing is not null)
        {
            existing.Slug = slug;
            existing.UpdatedAt = updatedAt;
        }
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var removed = _pages.RemoveAll(p => p.Id == id) > 0;
        return Task.FromResult(removed);
    }
}

/// <summary>In-memory IPageVersionStore: one draft per page + an append-only published history.</summary>
public sealed class InMemoryPageVersionStore : IPageVersionStore
{
    private readonly List<PageVersionRecord> _versions = new();

    public Task<PageVersionRecord?> GetDraftAsync(string pageId, CancellationToken ct = default) =>
        Task.FromResult(_versions.FirstOrDefault(v => v.PageId == pageId && v.Status == PageVersionStatus.Draft));

    public Task SaveDraftAsync(PageVersionRecord draft, CancellationToken ct = default)
    {
        _versions.RemoveAll(v => v.PageId == draft.PageId && v.Status == PageVersionStatus.Draft);
        _versions.Add(draft);
        return Task.CompletedTask;
    }

    public Task DeleteDraftAsync(string pageId, CancellationToken ct = default)
    {
        _versions.RemoveAll(v => v.PageId == pageId && v.Status == PageVersionStatus.Draft);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PageVersionRecord>> GetHistoryAsync(string pageId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageVersionRecord>>(
            _versions.Where(v => v.PageId == pageId && v.Status != PageVersionStatus.Draft)
                     .OrderByDescending(v => v.Number).ToList());

    public Task<PageVersionRecord?> GetVersionAsync(string versionId, CancellationToken ct = default) =>
        Task.FromResult(_versions.FirstOrDefault(v => v.VersionId == versionId));

    public Task AppendPublishedAsync(PageVersionRecord version, int keepLast, CancellationToken ct = default)
    {
        _versions.Add(version);
        if (keepLast > 0)
        {
            var keep = _versions.Where(v => v.PageId == version.PageId && v.Status != PageVersionStatus.Draft)
                                .OrderByDescending(v => v.Number).Take(keepLast).ToHashSet();
            _versions.RemoveAll(v => v.PageId == version.PageId && v.Status != PageVersionStatus.Draft && !keep.Contains(v));
        }
        return Task.CompletedTask;
    }

    public Task DeleteForPageAsync(string pageId, CancellationToken ct = default)
    {
        _versions.RemoveAll(v => v.PageId == pageId);
        return Task.CompletedTask;
    }
}

/// <summary>No-op unit of work / transaction — the in-memory store mutates directly.</summary>
public sealed class NoopUnitOfWork : IUnitOfWork
{
    public Task<IStorageTransaction> BeginAsync(CancellationToken ct = default) =>
        Task.FromResult<IStorageTransaction>(new NoopTransaction());

    private sealed class NoopTransaction : IStorageTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly List<UserRecord> _users = new();

    public IReadOnlyList<UserRecord> Users => _users;

    public Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Username == username));

    public Task<UserRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UserRecord>>(_users.ToList());

    public Task InsertAsync(UserRecord user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task<UserRecord?> FindByExternalAsync(string provider, string externalId, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Provider == provider && u.ExternalId == externalId));

    public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email == email));

    public Task UpdateAsync(UserRecord user, CancellationToken ct = default)
    {
        var idx = _users.FindIndex(u => u.Id == user.Id);
        if (idx >= 0) _users[idx] = user;
        return Task.CompletedTask;
    }
}
