using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstandingDotNet.Tests
{
    public class FieldExtractionIntegrationTest
    {
        private readonly IFieldExtractionService service;

        public FieldExtractionIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint")))
                    {
                        throw new ArgumentException("Endpoint must be provided in appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion")))
                    {
                        throw new ArgumentException("API version must be provided in appsettings.json.");
                    }
                    services.AddConfigurations(opts =>
                    {
                        context.Configuration.GetSection("AZURE_CU_CONFIG").Bind(opts);
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
        /// Executes an integration test for field extraction using predefined templates and sample files.
        /// </summary>
        /// <remarks>This test validates the functionality of the field extraction service by creating and
        /// using analyzers with various templates and sample files. It ensures that the service produces valid results
        /// and does not throw exceptions during execution.  The test uses multiple predefined templates, including
        /// invoice processing, call recording analytics, conversational audio analytics, and marketing video analysis.
        /// Each template is paired with a corresponding sample file to simulate real-world usage scenarios.</remarks>
        /// <returns></returns>
        [Fact]
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

                string field_extraction_analyzerId = $"field-extraction-sample-{Guid.NewGuid()}";

                foreach (var item in ExtractionTemplates)
                {
                    var (analyzerTemplatePath, analyzerSampleFilePath) = ExtractionTemplates[item.Key];
                    JsonDocument resultJson = await service.CreateAndUseAnalyzer(field_extraction_analyzerId, analyzerTemplatePath, analyzerSampleFilePath);

                    Assert.NotNull(resultJson);
                    Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
                    Assert.True(result.TryGetProperty("warnings", out var values));
                    Assert.False(values.EnumerateArray().Any(), "The warnings array should be empty");
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
