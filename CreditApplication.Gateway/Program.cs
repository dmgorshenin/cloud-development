using CreditApplication.Gateway.LoadBalancing;
using CreditApplication.ServiceDefaults;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddGatewayDefaults();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var generatorNames = builder.Configuration.GetSection("GeneratorServices").Get<string[]>() ?? [];
var addressOverrides = new List<KeyValuePair<string, string?>>();
for (var i = 0; i < generatorNames.Length; i++)
{
    var url = builder.Configuration[$"services:{generatorNames[i]}:http:0"];
    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Host", uri.Host));
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Port", uri.Port.ToString()));
    }
}
if (addressOverrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(addressOverrides);

var weights = builder.Configuration
    .GetSection("ReplicaWeights")
    .Get<Dictionary<string, double>>() ?? new Dictionary<string, double>();

builder.Services
    .AddOcelot(builder.Configuration)
    .AddCustomLoadBalancer((route, serviceDiscovery) =>
        new WeightedRandomLoadBalancer(serviceDiscovery, weights));

var app = builder.Build();

app.UseCors();

app.UseHealthChecks("/health");
app.UseHealthChecks("/alive", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

await app.UseOcelot();

app.Run();
