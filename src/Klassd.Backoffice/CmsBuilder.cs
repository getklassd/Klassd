using Klassd.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Backoffice;

/// <summary>
/// Default <see cref="ICmsBuilder"/> returned by <c>AddKlassd()</c>.
/// Storage adapter packages extend this with <c>UseMongoDb()</c> / <c>UsePostgres()</c>.
/// </summary>
public sealed class CmsBuilder(IServiceCollection services, IConfiguration configuration) : ICmsBuilder
{
    public IServiceCollection Services { get; } = services;
    public IConfiguration Configuration { get; } = configuration;
}
