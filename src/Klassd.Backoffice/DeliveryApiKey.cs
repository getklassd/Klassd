using System.Security.Cryptography;
using System.Text;

namespace Klassd.Backoffice;

/// <summary>Outcome of evaluating a delivery request against the API-key policy.</summary>
public enum DeliveryAccess
{
    /// <summary>API key not required — delivery is public.</summary>
    Public,
    /// <summary>A valid key was supplied.</summary>
    Authorized,
    /// <summary>A key is required but the supplied one is missing/wrong.</summary>
    Unauthorized,
    /// <summary>A key is required but none is configured on the server (misconfiguration).</summary>
    NotConfigured,
}

/// <summary>Pure delivery API-key decision, separated from the endpoint filter so it is unit-testable.</summary>
public static class DeliveryApiKey
{
    public static DeliveryAccess Evaluate(bool require, string? configuredKey, string? providedKey)
    {
        if (!require) return DeliveryAccess.Public;
        if (string.IsNullOrEmpty(configuredKey)) return DeliveryAccess.NotConfigured;
        return !string.IsNullOrEmpty(providedKey) && FixedTimeEquals(providedKey, configuredKey)
            ? DeliveryAccess.Authorized
            : DeliveryAccess.Unauthorized;
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

/// <summary>
/// Endpoint filter on the public delivery GETs. No-op when the API key isn't required (public mode);
/// otherwise validates the configured key from the request header.
/// </summary>
internal sealed class DeliveryApiKeyFilter(CmsOptions options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[options.DeliveryApiKeyHeader].ToString();
        return DeliveryApiKey.Evaluate(options.RequireDeliveryApiKey, options.DeliveryApiKey, provided) switch
        {
            DeliveryAccess.Public or DeliveryAccess.Authorized => await next(context),
            DeliveryAccess.NotConfigured => Results.Problem(
                "Delivery API key is required but not configured (Klassd:Delivery:ApiKey).", statusCode: 503),
            _ => Results.Unauthorized(),
        };
    }
}
