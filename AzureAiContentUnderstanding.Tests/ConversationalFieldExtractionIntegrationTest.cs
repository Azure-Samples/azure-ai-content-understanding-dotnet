using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ConversationalFieldExtraction.Interfaces;
using ConversationalFieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    public class ConversationalConversationalFieldExtractionIntegrationTest
    {
        private readonly IConversationalFieldExtractionService service;

        public ConversationalConversationalFieldExtractionIntegrationTest()
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
                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/conversational_field_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IConversationalFieldExtractionService, ConversationalFieldExtractionService>();
                })
                .Build();

            service = host.Services.GetService<IConversationalFieldExtractionService>()!;
        }

        [Fact(DisplayName = "Conversational Field Extraction Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            try
            {
                var ExtractionTemplates = new Dictionary<string, (string, string)>
                {
                    { "call_recording_pretranscribe_batch", ("./analyzer_templates/call_recording_analytics_text.json", "./data/batch_pretranscribed.json") },
                    { "call_recording_pretranscribe_fast", ("./analyzer_templates/call_recording_analytics_text.json", "./data/fast_pretranscribed.json") },
                    { "call_recording_pretranscribe_cu", ("./analyzer_templates/call_recording_analytics_text.json", "./data/cu_pretranscribed.json") }
                };
                var analyzerId = $"conversational-field-extraction-sample-{Guid.NewGuid()}";

                foreach (var item in ExtractionTemplates)
                {
                    // Extract the template path and sample file path from the dictionary
                    var (analyzerTemplatePath, analyzerSampleFilePath) = ExtractionTemplates[item.Key];

                    // Create the analyzer from the template
                    await CreateAnalyzerFromTemplateAsync(analyzerId, analyzerTemplatePath);

                    // Extract fields using the created analyzer
                    await ExtractFieldsWithAnalyzerAsync(analyzerId, analyzerSampleFilePath);

                    // Clean up the analyzer after use
                    await service.DeleteAnalyzerAsync(analyzerId);
                }
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // Assert that no exceptions were thrown during the test.
            Assert.Null(serviceException);
        }

        private async Task CreateAnalyzerFromTemplateAsync(string analyzerId, string analyzerTemplatePath)
        {
            // Implementation for creating an analyzer from a template
            var resultJson = await service.CreateAnalyzerFromTemplateAsync(analyzerId, analyzerTemplatePath);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var warnings));
            Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
            Assert.True(result.TryGetProperty("status", out JsonElement status));
            Assert.Equal("ready", status.ToString());
            Assert.True(result.TryGetProperty("mode", out JsonElement mode));
            Assert.Equal("standard", mode.ToString());
            Assert.True(result.TryGetProperty("fieldSchema", out JsonElement fieldSchema));
            Assert.True(fieldSchema.TryGetProperty("fields", out JsonElement fields));
            Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));
        }

        private async Task ExtractFieldsWithAnalyzerAsync(string analyzerId, string analyzerSampleFilePath)
        {
            // Implementation for extracting fields using the created analyzer
            var resultJson = await service.ExtractFieldsWithAnalyzerAsync(analyzerId, analyzerSampleFilePath);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var warnings));
            Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
            Assert.True(result.TryGetProperty("contents", out JsonElement contents));
            Assert.True(contents[0].TryGetProperty("markdown", out JsonElement markdown));
            Assert.True(!string.IsNullOrWhiteSpace(markdown.GetRawText()));
            Assert.True(contents[0].TryGetProperty("fields", out JsonElement fields));
            Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));
        }
    }
}
