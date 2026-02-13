using CreditApplication.Generator.Services;
using CreditApplication.ServiceDefaults;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("redis");

builder.Services.AddSingleton<CreditApplicationGenerator>();
builder.Services.AddScoped<CreditApplicationService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseSerilogRequestLogging();

app.MapDefaultEndpoints();

// API endpoint для получения кредитной заявки
app.MapGet("/credit-application", async (
    int id,
    CreditApplicationService service,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("Received request for credit application with ID: {Id}", id);
    
    if (id <= 0)
    {
        logger.LogWarning("Received invalid ID: {Id}", id);
        return Results.BadRequest(new { error = "ID must be a positive number" });
    }
    
    try
    {
        var application = await service.GetByIdAsync(id, cancellationToken);
        return Results.Ok(application);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while getting credit application {Id}", id);
        return Results.Problem("An error occurred while processing the request");
    }
})
.WithName("GetCreditApplication");

app.Run();
