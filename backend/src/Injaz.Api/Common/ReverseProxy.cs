using Microsoft.AspNetCore.HttpOverrides;

namespace Injaz.Api.Common;

/// <summary>Bound from <c>ReverseProxy</c>.</summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Networks (CIDR) the reverse proxy connects from. Only requests from these addresses may set the client IP
    /// and scheme through <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c>. Empty = not behind a proxy.
    /// </summary>
    public string[] TrustedNetworks { get; set; } = [];
}

internal static class ReverseProxySetup
{
    /// <remarks>
    /// In production nginx runs on the host and reaches the container through Docker's bridge network, so the API
    /// sees the bridge gateway (a private address) as the peer. Trusting the private ranges is safe because the
    /// container port is published on 127.0.0.1 only: nothing outside the server can connect to it directly.
    /// Without this, the client IP (rate limiting) would be nginx's address for every request.
    /// </remarks>
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new ReverseProxyOptions();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1; // exactly one proxy (nginx) in front of the API
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var network in settings.TrustedNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }
}
