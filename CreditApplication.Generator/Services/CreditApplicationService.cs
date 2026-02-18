using CreditApplication.Generator.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CreditApplication.Generator.Services;

/// <summary>
/// Сервис кредитных заявок с поддержкой кэширования
/// </summary>
public class CreditApplicationService(
    CreditApplicationGenerator generator,
    IDistributedCache cache,
    IConfiguration configuration,
    ILogger<CreditApplicationService> logger)
{
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(
        configuration.GetValue("CacheSettings:ExpirationMinutes", 5));
    private const string CacheKeyPrefix = "credit-application:";

    /// <summary>
    /// Получает кредитную заявку по ID.
    /// При первом запросе генерирует и кэширует, при повторном - возвращает из кэша.
    /// </summary>
    public async Task<CreditApplicationModel> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{id}";

        logger.LogInformation("Request for credit application with ID: {Id}", id);

        var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

        if (!string.IsNullOrEmpty(cachedData))
        {
            logger.LogInformation("Credit application {Id} found in cache", id);

            CreditApplicationModel? cachedApplication = null;
            try
            {
                cachedApplication = JsonSerializer.Deserialize<CreditApplicationModel>(cachedData);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize cached credit application {Id}, regenerating", id);
            }

            if (cachedApplication is not null)
                return cachedApplication;

            logger.LogWarning("Cached data for credit application {Id} deserialized to null, regenerating", id);
        }

        logger.LogInformation("Credit application {Id} not found in cache, generating new one", id);

        var application = generator.Generate(id);

        var serializedData = JsonSerializer.Serialize(application);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheExpiration
        };

        await cache.SetStringAsync(cacheKey, serializedData, cacheOptions, cancellationToken);

        logger.LogInformation(
            "Credit application {Id} saved to cache with TTL {CacheExpiration} minutes",
            id,
            _cacheExpiration.TotalMinutes);

        return application;
    }
}
