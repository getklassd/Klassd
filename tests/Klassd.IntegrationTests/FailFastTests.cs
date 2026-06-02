using Klassd.Backoffice;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.IntegrationTests;

public class FailFastTests
{
    /// <summary>
    /// Forgetting a storage adapter must fail fast at MapKlassd with a clear message,
    /// not an opaque "cannot resolve IPageStore" later. The adapter guard runs before any
    /// real endpoint mapping, so a minimal fake IEndpointRouteBuilder is enough to trigger it.
    /// (The happy path — with an adapter — is covered end-to-end by the UI tests, which boot
    /// a real WebApplication where MapStaticAssets/MapRazorComponents can run.)
    /// </summary>
    [Test]
    public async Task MapKlassd_without_a_storage_adapter_throws()
    {
        var services = new ServiceCollection();
        services.AddKlassd(new ConfigurationBuilder().Build()); // engine registered, but NO .UseXxx adapter

        await using var provider = services.BuildServiceProvider();
        var endpoints = new TestEndpointRouteBuilder(provider);

        await Assert.That(() => { endpoints.MapKlassd(); })
            .Throws<InvalidOperationException>();
    }

    /// <summary>Minimal IEndpointRouteBuilder — MapKlassd's adapter guard only uses ServiceProvider.</summary>
    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IApplicationBuilder CreateApplicationBuilder() => throw new NotSupportedException();
    }
}
