# Klassd examples

Worked, **compilable** examples of Klassd's two main extension points. The engine depends only on
interfaces in `Klassd.Abstractions` — never on a concrete database or cloud SDK — so you can target
any backend by implementing an interface and adding a `UseXxx` registration extension.

These projects are intentionally **not** part of `Klassd.slnx` and are **not packable**; they're
reference material you can copy from. Each references `Klassd.Abstractions` via a project reference.

## [`InMemoryMediaAdapter`](InMemoryMediaAdapter) — a custom media (blob) backend

The smallest possible adapter. A media backend is:

1. **`IBlobStore`** — three methods: `PutAsync`, `OpenReadAsync` (null if missing), `DeleteAsync`.
   Bytes only; content type and metadata live in `IMediaStore`.
2. **A `UseXxx` extension** on `IMediaSectionBuilder` that registers the blob store **keyed by the
   section name** (`builder.Name`). That key is how the engine resolves the right backend per section.

| File | Role |
|------|------|
| `InMemoryBlobStore.cs` | the `IBlobStore` implementation |
| `InMemoryBlobExtensions.cs` | `UseInMemoryBlobs()` — the registration seam |

To target Azure Blob Storage or an in-house object store, keep this exact shape and swap the
dictionary for your SDK's client.

## [`InMemoryStorageAdapter`](InMemoryStorageAdapter) — a custom database backend

A complete storage adapter implementing every persistence interface the engine resolves.

| Interface | Purpose |
|-----------|---------|
| `IPageStore` | pages (CRUD, by-locale / by-content / children, slug lookup) |
| `IMediaStore` | media metadata (bytes live in the blob store) |
| `IDictionaryStore` | translation dictionary entries |
| `IUserStore` / `IPreferencesStore` | backoffice accounts + per-user UI prefs |
| `IUnitOfWork` | atomicity seam for multi-record ops (real DBs → a transaction) |
| `IStorageInitializer` | one-time schema setup at startup (idempotent) |

| File | Role |
|------|------|
| `InMemoryDatabase.cs` | shared singleton holding the collections + defensive `Clone` helpers |
| `InMemoryPageStore.cs` / `InMemoryMediaStore.cs` / `InMemoryDictionaryStore.cs` / `InMemoryUserStore.cs` | the stores |
| `InMemoryUnitOfWork.cs` | no-op transaction (best-effort, like Mongo single-node) |
| `InMemoryStorageInitializer.cs` | no-op (nothing to create in memory) |
| `InMemoryCmsBuilderExtensions.cs` | `UseInMemoryStorage()` — registers all of the above |

> **Why clone on read and write?** A real DB adapter gets isolation for free (it serializes to/from
> rows or documents). An in-memory store hands back live references, so it must deep-copy or callers
> would mutate stored state. See `InMemoryDatabase.cs`.

For production-grade references, see the shipped adapters under `src/`:
`Klassd.Data.Sqlite` / `.Data.Postgres` / `.Data.MongoDb` and
`Klassd.Media.FileSystem` / `.Media.S3` / `.Media.GoogleCloud`.

## Building

```bash
dotnet build examples/InMemoryMediaAdapter/InMemoryMediaAdapter.csproj
dotnet build examples/InMemoryStorageAdapter/InMemoryStorageAdapter.csproj
```
