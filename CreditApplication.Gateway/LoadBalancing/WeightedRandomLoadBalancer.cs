using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace CreditApplication.Gateway.LoadBalancing;

public sealed class WeightedRandomLoadBalancer : ILoadBalancer
{
    private readonly IServiceDiscoveryProvider _serviceDiscovery;
    private readonly IReadOnlyDictionary<string, double> _weights;

    public WeightedRandomLoadBalancer(
        IServiceDiscoveryProvider serviceDiscovery,
        IReadOnlyDictionary<string, double> weights)
    {
        _serviceDiscovery = serviceDiscovery;
        _weights = weights;
    }

    public async Task<Response<ServiceHostAndPort>> Lease(HttpContext httpContext)
    {
        var services = await _serviceDiscovery.GetAsync();

        if (services is null)
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreNullError("Service discovery returned null"));

        if (services.Count == 0)
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreNullError("No downstream services are available"));

        return new OkResponse<ServiceHostAndPort>(SelectByWeight(services).HostAndPort);
    }

    private Service SelectByWeight(IList<Service> services)
    {
        var weighted = services
            .Select(s => (
                Service: s,
                Weight: _weights.TryGetValue(
                    $"{s.HostAndPort.DownstreamHost}:{s.HostAndPort.DownstreamPort}",
                    out var w) ? w : 1.0))
            .ToList();

        var total = weighted.Sum(x => x.Weight);

        // If all weights are zero or missing, fall back to uniform random selection.
        if (total <= 0)
            return services[Random.Shared.Next(services.Count)];

        var roll = Random.Shared.NextDouble() * total;
        var cumulative = 0.0;

        foreach (var (service, weight) in weighted)
        {
            cumulative += weight;
            if (roll < cumulative)
                return service;
        }

        // Guard against floating-point rounding: cumulative may fall infinitesimally short of total.
        return weighted[^1].Service;
    }

    // No connection tracking needed for stateless weighted random load balancing.
    public void Release(ServiceHostAndPort hostAndPort) { }
}
