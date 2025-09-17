using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration tests for analyzer management functionality using IManagementService.
    /// Validates analyzer creation, retrieval of details, listing, and deletion.
    /// </summary>
    public class ManagementIntegrationTest
    {
        private readonly IManagementService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations for analyzer management.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CONTENT_UNDERSTANDING_ENDPOINT" or "AZURE_APIVERSION" are missing.
        /// </exception>
        public ManagementIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Load configuration from environment variables or appsettings.json
                    string? endpoint = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") ?? context.Configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_ENDPOINT");

                    // API version for Azure Content Understanding service
                    string? apiVersion = Environment.GetEnvironmentVariable("AZURE_APIVERSION") ?? context.Configuration.GetValue<string>("AZURE_APIVERSION");

                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        throw new ArgumentException("Endpoint must be provided in environment variable or appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(apiVersion))
                    {
                        throw new ArgumentException("API version must be provided in environment variable or appsettings.json.");
                    }

                    services.AddConfigurations(opts =>
                    {
                        opts.Endpoint = endpoint;
                        opts.ApiVersion = apiVersion;
                        opts.SubscriptionKey = context.Configuration.GetValue<string>("AZURE_SUBSCRIPTION_ID") ?? "";

                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/management";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IManagementService, ManagementService>();
                })
                .Build();

            service = host.Services.GetService<IManagementService>()!;
        }

        /// <summary>
        /// Runs the analyzer management workflow:
        /// 1. Creates an analyzer.
        /// 2. Retrieves and validates analyzer details.
        /// 3. Lists all analyzers.
        /// 4. Deletes the created analyzer.
        /// Ensures no exceptions occur and validates expected structure and content of responses.
        /// </summary>
        [Fact(DisplayName = "Management Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                var id = $"analyzer-management-sample-{Guid.NewGuid()}";
                var analyzerTemplatePath = "./analyzer_templates/call_recording_analytics.json";

                // Step 1: Create a simple analyzer
                var analyzerId = await service!.CreateAnalyzerAsync(id, analyzerTemplatePath);
                Assert.NotNull(analyzerId);

                // Step 2: Get analyzer details and validate structure/content
                Dictionary<string, object> details = await service.GetAnalyzerDetailsAsync(analyzerId);
                Assert.True(details.Any());
                Assert.True(details.ContainsKey("warnings"));
                Assert.True(details.TryGetValue("warnings", out var warnings));
                Assert.False(((JsonElement)warnings).EnumerateArray().Any(), "The warnings array should be empty");
                Assert.True(details.ContainsKey("mode"));
                Assert.Equal("standard", details["mode"].ToString());
                Assert.True(details.ContainsKey("status"));
                Assert.Equal("ready", details["status"].ToString());
                Assert.True(details.ContainsKey("fieldSchema"));
                Assert.True(((JsonElement)details["fieldSchema"]).TryGetProperty("fields", out var fields));
                Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));

                // Step 3: List all analyzers (verifies listing API, no assertion)
                var analyzers = await service.ListAnalyzersAsync();
                var analyzerIds = analyzers?.Select(s => s.GetProperty("analyzerId").ToString());
                Assert.True(analyzerIds?.Any(s => s.Equals(analyzerId)), "Created analyzer should be in the list of analyzers");

                // Step 4: Delete analyzer
                HttpResponseMessage response = await service.DeleteAnalyzerAsync(analyzerId);
                Assert.NotNull(response);
                Assert.True(response.IsSuccessStatusCode, "Delete operation should succeed");

                var analyzersAfterDelete = await service.ListAnalyzersAsync();
                var analyzerIdsAfterDelete = analyzersAfterDelete?.Select(s => s.GetProperty("analyzerId").ToString());
                Assert.False(analyzerIdsAfterDelete?.Any(s => s.Equals(analyzerId)), "Deleted analyzer should not be in the list of analyzers");
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // Final assertion: No exceptions should be thrown during workflow
            Assert.Null(serviceException);
        }
    }
}
