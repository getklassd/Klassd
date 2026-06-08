using Klassd.Abstractions.Storage;

namespace Klassd.Examples.InMemoryStorage;

/// <summary><see cref="ISettingsStore"/> — durable key/value system settings.</summary>
public sealed class InMemorySettingsStore(InMemoryDatabase db) : ISettingsStore
{
    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(db.Settings.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        db.Settings[key] = value;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(db.Settings.TryRemove(key, out _));
}
