using HotChocolate.Execution.Configuration;
using Klassd.Abstractions;
using Klassd.Backoffice;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.GraphQL;

public static class GraphQLExtensions
{
    /// <summary>
    /// Adds an opt-in GraphQL delivery server exposing pages, globals and locales. Call after
    /// <c>AddKlassd()</c>, then map the endpoint with <see cref="MapKlassdGraphQL"/>. Use
    /// <paramref name="configure"/> for HotChocolate options (e.g. complexity limits, persisted queries).
    /// </summary>
    public static ICmsBuilder UseGraphQL(this ICmsBuilder cms, Action<IRequestExecutorBuilder>? configure = null)
    {
        var builder = cms.Services.AddGraphQLServer().AddQueryType<Query>();
        configure?.Invoke(builder);
        return cms;
    }

    /// <summary>
    /// Maps the GraphQL endpoint (default <c>/graphql</c>) as anonymous content delivery under the
    /// Klassd delivery CORS policy — read-only, matching the REST delivery GETs.
    /// </summary>
    public static void MapKlassdGraphQL(this IEndpointRouteBuilder routes, string path = "/graphql")
    {
        routes.MapGraphQL(path)
            .AllowAnonymous()
            .RequireCors(KlassdExtensions.DeliveryCorsPolicy);
    }
}
