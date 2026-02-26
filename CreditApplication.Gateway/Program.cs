using CreditApplication.Gateway.LoadBalancing;
using CreditApplication.ServiceDefaults;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddGatewayDefaults();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var generatorNames = builder.Configuration.GetSection("GeneratorServices").Get<string[]>() ?? [];
var serviceWeights = builder.Configuration
    .GetSection("ReplicaWeights")
    .Get<Dictionary<string, double>>() ?? [];

var addressOverrides = new List<KeyValuePair<string, string?>>();
var hostPortToName = new Dictionary<string, string>();

for (var i = 0; i < generatorNames.Length; i++)
{
    var name = generatorNames[i];
    var url = builder.Configuration[$"services:{name}:http:0"];

    string resolvedHost, resolvedPort;
    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        resolvedHost = uri.Host;
        resolvedPort = uri.Port.ToString();
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Host", resolvedHost));
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Port", resolvedPort));
    }
    else
    {
        resolvedHost = builder.Configuration[$"Routes:0:DownstreamHostAndPorts:{i}:Host"] ?? "localhost";
        resolvedPort = builder.Configuration[$"Routes:0:DownstreamHostAndPorts:{i}:Port"] ?? "0";
    }

    if (serviceWeights.ContainsKey(name))
        hostPortToName[$"{resolvedHost}:{resolvedPort}"] = name;
}

if (addressOverrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(addressOverrides);

builder.Services
    .AddOcelot(builder.Configuration)
    .AddCustomLoadBalancer((route, serviceDiscovery) =>
        new WeightedRandomLoadBalancer(serviceDiscovery, serviceWeights, hostPortToName));

var app = builder.Build();

app.UseCors(Extensions.CorsPolicyName);

app.UseHealthChecks("/health");
app.UseHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });

await app.UseOcelot();

app.Run();
