using System.Text.Json.Serialization;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Globals;
using Klassd.Backoffice.Modules.Media;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Preferences.Models;
using Klassd.Core.Localization;
using Klassd.Core.Models;

namespace Klassd.Backoffice;

/// <summary>
/// System.Text.Json source-generation context for the headless delivery + admin API. Registering the
/// concrete response/request types lets STJ use precompiled (de)serialization metadata instead of
/// runtime reflection on the hot delivery paths (/api/pages, /api/media, /api/globals, …). It's wired
/// in via the JSON resolver chain in AddKlassd, ahead of the reflection resolver — so any type not
/// listed here still serializes (just not source-generated). Naming policy mirrors the API (camelCase).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
// ── Delivery (read) ───────────────────────────────────────────────────────────
[JsonSerializable(typeof(IReadOnlyList<PageRecord>))]
[JsonSerializable(typeof(PageRecord))]
[JsonSerializable(typeof(BlockInstanceRecord))]
[JsonSerializable(typeof(IReadOnlyList<MediaRecord>))]
[JsonSerializable(typeof(MediaRecord))]
[JsonSerializable(typeof(IReadOnlyList<MediaSection>))]
[JsonSerializable(typeof(GlobalDeliveryResponse))]
[JsonSerializable(typeof(IReadOnlyList<GlobalSummary>))]
[JsonSerializable(typeof(IReadOnlyList<DictionaryEntryRecord>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(UserPreferencesRecord))]
// ── Metadata ──────────────────────────────────────────────────────────────────
[JsonSerializable(typeof(IReadOnlyList<PageTypeInfo>))]
[JsonSerializable(typeof(IReadOnlyList<BlockTypeInfo>))]
[JsonSerializable(typeof(IReadOnlyList<GlobalTypeInfo>))]
[JsonSerializable(typeof(IReadOnlyList<LocaleDefinition>))]
// ── Requests (write) ──────────────────────────────────────────────────────────
[JsonSerializable(typeof(CreatePageRequest))]
[JsonSerializable(typeof(UpdatePageRequest))]
[JsonSerializable(typeof(UpdateMediaRequest))]
[JsonSerializable(typeof(UpdatePreferencesRequest))]
[JsonSerializable(typeof(List<BlockData>))]
public partial class KlassdJsonContext : JsonSerializerContext;
