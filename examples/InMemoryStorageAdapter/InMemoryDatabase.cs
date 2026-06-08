using System.Collections.Concurrent;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// The shared backing "database": one singleton holding every collection. The store classes are
/// thin facades over these dictionaries (a real adapter's stores are thin facades over a DB
/// connection). Registered as a singleton so data survives across request scopes.
/// </summary>
public sealed class InMemoryDatabase
{
    public ConcurrentDictionary<string, PageRecord> Pages { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, MediaRecord> Media { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, DictionaryEntryRecord> Dictionary { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, UserRecord> Users { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, UserPreferencesRecord> Preferences { get; } = new(StringComparer.Ordinal);
    public ConcurrentDictionary<string, string> Settings { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Defensive deep-copies. A real DB adapter gets isolation for free (it serializes to/from rows or
/// documents); an in-memory store must clone on read AND write, or callers would mutate the stored
/// instance through the reference they were handed.
/// </summary>
internal static class Clones
{
    public static PageRecord Clone(this PageRecord p) => new()
    {
        Id = p.Id,
        ContentId = p.ContentId,
        LocaleCode = p.LocaleCode,
        ParentId = p.ParentId,
        PageTypeName = p.PageTypeName,
        Name = p.Name,
        Slug = p.Slug,
        Data = new Dictionary<string, string>(p.Data),
        BlockAreas = p.BlockAreas.ToDictionary(
            area => area.Key,
            area => area.Value.Select(b => new BlockInstanceRecord
            {
                BlockTypeName = b.BlockTypeName,
                Data = new Dictionary<string, string>(b.Data),
                StartUtc = b.StartUtc,
                EndUtc = b.EndUtc,
                Priority = b.Priority,
            }).ToList()),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    public static MediaRecord Clone(this MediaRecord m) => new()
    {
        Id = m.Id,
        Section = m.Section,
        Key = m.Key,
        FileName = m.FileName,
        ContentType = m.ContentType,
        Size = m.Size,
        Width = m.Width,
        Height = m.Height,
        AltText = m.AltText,
        FocalPoints = m.FocalPoints.Select(f => new MediaFocalPoint { Breakpoint = f.Breakpoint, X = f.X, Y = f.Y }).ToList(),
        Data = new Dictionary<string, string>(m.Data),
        UploadedAt = m.UploadedAt,
    };

    public static DictionaryEntryRecord Clone(this DictionaryEntryRecord e) =>
        new() { Key = e.Key, Values = new Dictionary<string, string>(e.Values) };

    public static UserRecord Clone(this UserRecord u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        PasswordHash = u.PasswordHash,
        Provider = u.Provider,
        ExternalId = u.ExternalId,
        Disabled = u.Disabled,
    };

    public static UserPreferencesRecord Clone(this UserPreferencesRecord p) =>
        new() { UserId = p.UserId, SelectedLocale = p.SelectedLocale, Collapsed = new List<string>(p.Collapsed) };
}
