using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

if (builder.Environment.IsDevelopment())
    redis.WithRedisCommander();

var generator1 = builder.AddProject<Projects.CreditApplication_Generator>("generator-1")
    .WithEndpoint("http", endpoint => endpoint.Port = 5101)
    .WithHttpHealthCheck("/health")
    .WithReference(redis);

var generator2 = builder.AddProject<Projects.CreditApplication_Generator>("generator-2")
    .WithEndpoint("http", endpoint => endpoint.Port = 5102)
    .WithHttpHealthCheck("/health")
    .WithReference(redis);

var generator3 = builder.AddProject<Projects.CreditApplication_Generator>("generator-3")
    .WithEndpoint("http", endpoint => endpoint.Port = 5103)
    .WithHttpHealthCheck("/health")
    .WithReference(redis);

var gateway = builder.AddProject<Projects.CreditApplication_Gateway>("gateway")
    .WithEndpoint("http", endpoint => endpoint.Port = 5200)
    .WithHttpHealthCheck("/health")
    .WithReference(generator1)
    .WithReference(generator2)
    .WithReference(generator3)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway);

builder.Build().Run();
