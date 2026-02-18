using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

if (builder.Environment.IsDevelopment())
    redis.WithRedisCommander();

var generator1 = builder.AddProject<Projects.CreditApplication_Generator>("generator-1")
    .WithEndpoint("http", endpoint => endpoint.Port = 5101)
    .WithReference(redis);

var generator2 = builder.AddProject<Projects.CreditApplication_Generator>("generator-2")
    .WithEndpoint("http", endpoint => endpoint.Port = 5102)
    .WithReference(redis);

var generator3 = builder.AddProject<Projects.CreditApplication_Generator>("generator-3")
    .WithEndpoint("http", endpoint => endpoint.Port = 5103)
    .WithReference(redis);

var gateway = builder.AddProject<Projects.CreditApplication_Gateway>("gateway")
    .WithEndpoint("http", endpoint => endpoint.Port = 5200)
    .WithReference(generator1)
    .WithReference(generator2)
    .WithReference(generator3)
    .WithExternalHttpEndpoints()
    .WaitFor(generator1)
    .WaitFor(generator2)
    .WaitFor(generator3);

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
