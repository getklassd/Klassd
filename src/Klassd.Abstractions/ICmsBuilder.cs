using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Abstractions;

/// <summary>
/// Returned by <c>AddKlassd()</c>. Storage adapters (in their own packages)
/// extend this with <c>UseMongoDb()</c> / <c>UsePostgres()</c> so the consuming app
/// chooses a backend without the engine referencing any database package.
/// </summary>
public interface ICmsBuilder
{
    IServiceCollection Services { get; }
    IConfiguration Configuration { get; }
}
