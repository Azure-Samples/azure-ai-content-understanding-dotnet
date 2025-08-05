using ContentExtraction.Interfaces;
using ContentExtraction.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstandingDotNet.Tests
{
    public class ContentExtractionIntegrationTest
    {
        private readonly IContentExtractionService service;

        public ContentExtractionIntegrationTest()
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/content_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IContentExtractionService, ContentExtractionService>();
                })
                .Build();

            service = host.Services.GetService<IContentExtractionService>()!;
        }

        /// <summary>
        /// Tests the <see cref="Service.AnalyzeDocumentAsync"/> method to ensure it processes a document file correctly
        /// and returns a valid JSON result.
        /// </summary>
        /// <remarks>This test verifies that the <see cref="Service.AnalyzeDocumentAsync"/> method does
        /// not throw exceptions, produces a non-null JSON result, and includes expected fields such as "result",
        /// "contents", "markdown", and "tables" in the output.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task RunAnalyzeDocumentAsync()
        {
            Exception? serviceException = null;
            JsonDocument? resultJson = null;

            try
            {
                // Ensure the file path is correct and the file exists
                var docFilePath = "./data/invoice.pdf";
                Assert.True(File.Exists("./data/invoice.pdf"), "Document file does not exist at the specified path.");
                resultJson = await service.AnalyzeDocumentAsync(docFilePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // no exception should be thrown
            Assert.Null(serviceException);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
            Assert.True(result.TryGetProperty("contents", out var contents), "The output JSON lacks the 'contents' field");
            Assert.True(contents.GetArrayLength() > 0, "The contents array is empty");

            var firstContent = contents[0];
            Assert.True(firstContent.TryGetProperty("markdown", out var markdown), "The output content lacks the 'markdown' field");
            Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()), "The markdown content is empty");
            Assert.True(firstContent.TryGetProperty("tables", out var tables), "The output content lacks the 'tables' field");
        }

        /// <summary>
        /// Tests the <see cref="AnalyzeAudioAsync"/> method to ensure it processes an audio file correctly and returns
        /// valid results.
        /// </summary>
        /// <remarks>This test verifies that the <see cref="AnalyzeAudioAsync"/> method does not throw
        /// exceptions, produces a non-null JSON result, and includes expected fields such as "result", "contents",
        /// "markdown", and "fields" in the output. It also checks that the "contents" array is not empty and that the
        /// "markdown" field contains non-empty content.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task RunAnalyzeAudioAsync()
        {
            Exception? serviceException = null;
            JsonDocument? resultJson = null;

            try
            {
                string filePath = "./data/audio.wav";
                // Ensure the file path is correct and the file exists
                Assert.True(File.Exists(filePath), "Audio file does not exist at the specified path.");
                resultJson = await service.AnalyzeAudioAsync(filePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // no exception should be thrown
            Assert.Null(serviceException);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
            Assert.True(result.TryGetProperty("contents", out var contents), "The output JSON lacks the 'contents' field");
            Assert.True(contents.GetArrayLength() > 0, "The contents array is empty");

            var firstContent = contents[0];
            Assert.True(firstContent.TryGetProperty("markdown", out var markdown), "The output content lacks the 'markdown' field");
            Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()), "The markdown content is empty");
            Assert.True(firstContent.TryGetProperty("fields", out var fields), "The output content lacks the 'fields' field");
        }

        /// <summary>
        /// Tests the <see cref="Service.AnalyzeVideoAsync"/> method to ensure it processes a video file correctly and
        /// returns a valid JSON result.
        /// </summary>
        /// <remarks>This test verifies that the <see cref="Service.AnalyzeVideoAsync"/> method does not
        /// throw exceptions, correctly analyzes the specified video file, and produces a JSON result containing
        /// expected fields. The test checks for the presence of the "result" and "contents" fields in the output JSON,
        /// as well as validates that the "contents" array is non-empty and contains valid "markdown" content.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task RunAnalyzeVideoAsync()
        {
            Exception? serviceException = null;
            JsonDocument? resultJson = null;
            try
            {
                string filePath = "./data/FlightSimulator.mp4";
                // Ensure the file path is correct and the file exists
                Assert.True(File.Exists(filePath), "Video file does not exist at the specified path.");
                resultJson = await service.AnalyzeVideoAsync(filePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // no exception should be thrown
            Assert.Null(serviceException);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
            Assert.True(result.TryGetProperty("contents", out var contents), "The output JSON lacks the 'contents' field");
            Assert.True(contents.GetArrayLength() > 0, "The contents array is empty");
            var firstContent = contents[0];
            Assert.True(firstContent.TryGetProperty("markdown", out var markdown), "The output content lacks the 'markdown' field");
            Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()), "The markdown content is empty");
            Assert.True(firstContent.TryGetProperty("fields", out var fields), "The output content lacks the 'fields' field");
        }

        /// <summary>
        /// Tests the <see cref="Service.AnalyzeVideoWithFaceAsync"/> method to ensure it processes a video file and
        /// returns valid JSON output containing face analysis results.
        /// </summary>
        /// <remarks>This test verifies that the method does not throw exceptions, correctly analyzes the
        /// video file,  and produces a JSON result with expected fields such as "result", "contents", "markdown", and
        /// "fields". It also checks that the "contents" array is not empty and contains valid data.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task RunAnalyzeVideoWithFaceAsync()
        {
            Exception? serviceException = null;
            JsonDocument? resultJson = null;
            try
            {
                string filePath = "./data/FlightSimulator.mp4";
                // Ensure the file path is correct and the file exists
                Assert.True(File.Exists(filePath), "Video file does not exist at the specified path.");
                resultJson = await service.AnalyzeVideoWithFaceAsync(filePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // no exception should be thrown
            Assert.Null(serviceException);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
            Assert.True(result.TryGetProperty("contents", out var contents), "The output JSON lacks the 'contents' field");
            Assert.True(contents.GetArrayLength() > 0, "The contents array is empty");
            var firstContent = contents[0];
            Assert.True(firstContent.TryGetProperty("markdown", out var markdown), "The output content lacks the 'markdown' field");
            Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()), "The markdown content is empty");
            Assert.True(firstContent.TryGetProperty("fields", out var fields), "The output content lacks the 'fields' field");
        }
    }
}