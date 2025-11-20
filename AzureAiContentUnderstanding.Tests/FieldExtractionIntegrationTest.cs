using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration tests for field extraction scenarios using IFieldExtractionService.
    /// Validates that analyzers built from various templates can process different sample files correctly,
    /// producing valid structured results and handling errors gracefully.
    /// </summary>
    public class FieldExtractionIntegrationTest
    {
        private readonly IFieldExtractionService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations for field extraction.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CONTENT_UNDERSTANDING_ENDPOINT" or "AZURE_APIVERSION" are missing.
        /// </exception>
        public FieldExtractionIntegrationTest()
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
                        opts.SubscriptionKey = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_KEY") ?? context.Configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_KEY") ?? "";

                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/field_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IFieldExtractionService, FieldExtractionService>();
                })
                .Build();

            service = host.Services.GetService<IFieldExtractionService>()!;
        }

        /// <summary>
        /// Runs integration tests for field extraction using multiple predefined templates and sample files.
        /// For each template/sample pair, verifies that the analyzer produces structured results with expected fields.
        /// </summary>
        /// <remarks>
        /// This test iterates through several field extraction scenarios:
        /// <list type="bullet">
        /// <item><description>Invoice extraction from PDF</description></item>
        /// <item><description>Call recording analytics from MP3</description></item>
        /// <item><description>Conversational audio analytics from MP3</description></item>
        /// <item><description>Marketing video analysis from MP4</description></item>
        /// </list>
        /// Each scenario ensures the service does not throw exceptions, produces a valid JSON result,
        /// and includes expected fields: "result", "contents", "markdown", and "fields".
        /// </remarks>
        [Fact(DisplayName = "Field Extraction Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            try
            {
                var ExtractionTemplates = new Dictionary<string, (string, string)>
                {
                    { "invoice", ("./analyzer_templates/invoice.json", "./data/invoice.pdf") },
                    { "call_recording", ("./analyzer_templates/call_recording_analytics.json", "./data/callCenterRecording.mp3") },
                    { "conversation_audio", ("./analyzer_templates/conversational_audio_analytics.json", "./data/callCenterRecording.mp3") },
                    { "marketing_video", ("./analyzer_templates/marketing_video.json", "./data/FlightSimulator.mp4") }
                };

                string analyzerId = $"field-extraction-sample-{Guid.NewGuid()}";

                foreach (var item in ExtractionTemplates)
                {
                    var (analyzerTemplatePath, analyzerSampleFilePath) = ExtractionTemplates[item.Key];
                    JsonDocument resultJson = await service.CreateAndUseAnalyzer(analyzerId, analyzerTemplatePath, analyzerSampleFilePath);

                    Assert.NotNull(resultJson);
                    Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
                    Assert.True(result.TryGetProperty("warnings", out var warnings));
                    Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
                    Assert.True(result.TryGetProperty("contents", out JsonElement contents));
                    Assert.True(contents.EnumerateArray().Any());
                    var content = contents[0];
                    Assert.True(content.TryGetProperty("markdown", out JsonElement markdown));
                    Assert.True(!string.IsNullOrWhiteSpace(markdown.ToString()));
                    Assert.True(content.TryGetProperty("fields", out JsonElement fields));
                    Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));
                }
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // Assert that no exceptions were thrown during the test.
            Assert.Null(serviceException);
        }
    }
}
