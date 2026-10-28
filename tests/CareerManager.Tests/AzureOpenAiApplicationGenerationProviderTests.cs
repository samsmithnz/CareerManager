using System.Net;
using System.Text.Json;
using CareerManager.Services;
using Microsoft.Extensions.Configuration;

namespace CareerManager.Tests;

public sealed class AzureOpenAiApplicationGenerationProviderTests
{
    [Fact]
    public async Task RemoteProviderSendsOnlyGenerationRequestAndParsesStructuredResponse()
    {
        var resultJson = JsonSerializer.Serialize(new ApplicationGenerationResult(
            [],
            [],
            [],
            [new SupportedClaim("Used SQL for reporting.", [4])],
            [new SupportedClaim("Used SQL for reporting.", [4])]));
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = resultJson } }
            }
        });
        string? submittedBody = null;
        string? submittedKey = null;
        var client = new HttpClient(new RecordingHandler(request =>
        {
            submittedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            submittedKey = request.Headers.GetValues("api-key").Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };
        }));
        var provider = new AzureOpenAiApplicationGenerationProvider(client, CreateConfiguration());
        var request = new ApplicationGenerationRequest(
            "Example",
            "Analyst",
            "Build reports.",
            "Company context",
            [new GenerationEvidence(4, "Skill", "SQL", "Used SQL for reporting.")]);

        var result = await provider.GenerateAsync(request, default);

        Assert.True(provider.SendsDataExternally);
        Assert.Equal("gpt-4o-mini", provider.ModelName);
        Assert.Equal("East US 2 (Virginia)", provider.Region);
        Assert.Equal("synthetic-test-key", submittedKey);
        Assert.Contains("Used SQL for reporting.", submittedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("private notes", submittedBody, StringComparison.Ordinal);
        Assert.Equal(4, Assert.Single(Assert.Single(result.ResumeClaims).EvidenceIds));
    }

    [Fact]
    public async Task RemoteRateLimitIsActionableAndProviderSetupRequiresTermsApproval()
    {
        var client = new HttpClient(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var provider = new AzureOpenAiApplicationGenerationProvider(client, CreateConfiguration());
        var error = await Assert.ThrowsAsync<ApplicationGenerationException>(() =>
            provider.GenerateAsync(new ApplicationGenerationRequest(
                "Example", "Analyst", string.Empty, string.Empty, []), default));

        Assert.Contains("rate limit", error.Message, StringComparison.OrdinalIgnoreCase);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationGeneration:AzureOpenAI:DataHandlingApproved"] = "false"
            })
            .Build();
        Assert.Throws<InvalidOperationException>(() =>
            AzureOpenAiApplicationGenerationProvider.AzureOpenAiOptions.FromConfiguration(configuration));
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationGeneration:AzureOpenAI:Endpoint"] = "https://career-resource.openai.azure.com",
                ["ApplicationGeneration:AzureOpenAI:Deployment"] = "gpt-4o-mini",
                ["ApplicationGeneration:AzureOpenAI:Region"] = "eastus2",
                ["ApplicationGeneration:AzureOpenAI:ApiKey"] = "synthetic-test-key",
                ["ApplicationGeneration:AzureOpenAI:DataHandlingApproved"] = "true"
            })
            .Build();

    [Fact]
    public void RemoteProviderRejectsRegionsOutsideTheSelectedEastCoastDeployment()
    {
        var settings = CreateConfiguration().AsEnumerable()
            .ToDictionary(setting => setting.Key, setting => setting.Value);
        settings["ApplicationGeneration:AzureOpenAI:Region"] = "australiaeast";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AzureOpenAiApplicationGenerationProvider.AzureOpenAiOptions.FromConfiguration(configuration));

        Assert.Contains("East US 2", exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
