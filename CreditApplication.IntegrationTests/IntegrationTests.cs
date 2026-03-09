using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace CreditApplication.IntegrationTests;

public class AppHostFixture : IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CreditApplication_AppHost>();

        appHost.Services.ConfigureHttpClientDefaults(http =>
            http.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            }));

        App = await appHost.BuildAsync();
        await App.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
    }
}

public class IntegrationTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    [Fact]
    public async Task ProduceAndRetrieveApplication_ThroughGateway_ReturnsCorrectId()
    {
        var testId = Random.Shared.Next(1, 100000);
        var httpClient = fixture.App.CreateHttpClient("gateway");
        using var response = await httpClient.GetAsync($"/credit-application?id={testId}");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var idElement = doc.RootElement.GetProperty("id");
        Assert.Equal(testId, idElement.GetInt32());
    }

    [Fact]
    public async Task RepeatedRequests_ThroughGateway_ReturnCachedResponse()
    {
        var testId = Random.Shared.Next(1, 100000);
        var httpClient = fixture.App.CreateHttpClient("gateway");

        using var response1 = await httpClient.GetAsync($"/credit-application?id={testId}");
        response1.EnsureSuccessStatusCode();
        var content1 = await response1.Content.ReadAsStringAsync();

        using var response2 = await httpClient.GetAsync($"/credit-application?id={testId}");
        response2.EnsureSuccessStatusCode();
        var content2 = await response2.Content.ReadAsStringAsync();

        var node1 = JsonNode.Parse(content1);
        var node2 = JsonNode.Parse(content2);

        Assert.True(JsonNode.DeepEquals(node1, node2), "Cached JSON responses should be semantically equal");
    }

    [Fact]
    public async Task ApplicationIsProcessedAndSavedToS3_AvailableInFilesList()
    {
        var testId = Random.Shared.Next(1, 100000);
        var httpClient = fixture.App.CreateHttpClient("gateway");

        using var genResponse = await httpClient.GetAsync($"/credit-application?id={testId}");
        genResponse.EnsureSuccessStatusCode();

        var expectedFileName = $"credit-application-{testId}.json";
        var fileFound = false;

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(6000);

            using var filesResponse = await httpClient.GetAsync("/files");
            if (filesResponse.IsSuccessStatusCode)
            {
                var filesContent = await filesResponse.Content.ReadAsStringAsync();
                var files = JsonSerializer.Deserialize<List<string>>(filesContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (files != null && files.Contains(expectedFileName))
                {
                    fileFound = true;
                    break;
                }
            }
        }

        Assert.True(fileFound, $"File {expectedFileName} should be present in file list");
    }

    [Fact]
    public async Task ApplicationSavedToS3_CanBeRetrievedByFileNameAndContentMatches()
    {
        var testId = Random.Shared.Next(1, 100000);
        var httpClient = fixture.App.CreateHttpClient("gateway");

        using var genResponse = await httpClient.GetAsync($"/credit-application?id={testId}");
        genResponse.EnsureSuccessStatusCode();
        var apiContent = await genResponse.Content.ReadAsStringAsync();

        var expectedFileName = $"credit-application-{testId}.json";
        string? fileContent = null;

        for (var i = 0; i < 60; i++)
        {
            await Task.Delay(2000);

            using var fileResponse = await httpClient.GetAsync($"/files/{expectedFileName}");
            if (fileResponse.IsSuccessStatusCode)
            {
                fileContent = await fileResponse.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(fileContent))
                {
                    break;
                }
            }
        }

        Assert.NotNull(fileContent);

        using var apiDoc = JsonDocument.Parse(apiContent);
        using var s3Doc = JsonDocument.Parse(fileContent);

        var apiId = apiDoc.RootElement.GetProperty("id").GetInt32();
        var s3Id = s3Doc.RootElement.GetProperty("Id").GetInt32();

        Assert.Equal(apiId, s3Id);
        Assert.Equal(testId, s3Id);
    }
}