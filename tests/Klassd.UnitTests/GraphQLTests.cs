using HotChocolate.Execution;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Core.Localization;
using Klassd.GraphQL;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.UnitTests;

public class GraphQLTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPageStore>(new InMemoryPageStore());
        services.AddSingleton<IUnitOfWork>(new NoopUnitOfWork());
        services.AddSingleton<IPageVersionStore>(new InMemoryPageVersionStore());
        services.AddSingleton(new LocaleRegistry([new LocaleDefinition("en", Mandatory: true)]));
        services.AddSingleton<PageService>();
        services.AddGraphQLServer().AddQueryType<Query>();
        return services.BuildServiceProvider();
    }

    // The resolvers are thin wrappers over PageService/PageDelivery/PageSchedule (covered by their own
    // tests); this verifies the opt-in package wires up and the delivery query surface is exposed.
    [Test]
    public async Task Schema_builds_and_exposes_the_delivery_queries()
    {
        await using var provider = BuildProvider();
        var executor = await provider.GetRequestExecutorAsync();
        var sdl = executor.Schema.ToString();

        await Assert.That(sdl).Contains("pages");
        await Assert.That(sdl).Contains("pageBySlug");
        await Assert.That(sdl).Contains("pageTranslations");
        await Assert.That(sdl).Contains("global");
        await Assert.That(sdl).Contains("locales");
        await Assert.That(sdl).Contains("type Page");
    }
}
