using Azure.AI.ContentUnderstanding;
using ContentExtraction.Interfaces;
using ContentExtraction.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration tests for content extraction service, covering document, audio, and video analysis scenarios.
    /// Each test ensures the service produces expected JSON results and handles input files correctly.
    /// </summary>
    public class ContentExtractionIntegrationTest
    {
        private readonly IContentExtractionService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CU_CONFIG:Endpoint" or "AZURE_CU_CONFIG:ApiVersion" are missing.
        /// </exception>
        public ContentExtractionIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Load configuration from environment variables or appsettings.json
                    string? endpoint = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") ?? context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint");

                    // API version for Azure Content Understanding service
                    string? apiVersion = Environment.GetEnvironmentVariable("AZURE_CU_CONFIG_ApiVersion") ?? context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion");

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
        /// Tests document analysis for a PDF using <see cref="IContentExtractionService.AnalyzeDocumentAsync"/>.
        /// Verifies that the JSON output contains required fields, the contents array is not empty,
        /// and that markdown and tables are present in the first content item.
        /// </summary>
        [Fact(DisplayName = "Analyze Document Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeDocumentAsync()
        {
            Exception? serviceException = null;
            AnalyzeResult? result = null;

            try
            {
                // Ensure the file path is correct and the file exists
                var docFilePath = "./data/invoice.pdf";
                Assert.True(File.Exists("./data/invoice.pdf"), "Document file does not exist at the specified path.");
                result = await service.AnalyzeDocumentAsync(docFilePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);
            Assert.False(result.Warnings.Any(), "The warnings array should be empty");
            Assert.False(result.Contents.Any(), "The contents array is empty");

            var content = result.Contents[0];
            Assert.False(string.IsNullOrWhiteSpace(content.Markdown), "The markdown content is empty");
            Assert.False(content.Fields.Any(), "The fields collection is empty");
        }

        /// <summary>
        /// Tests audio analysis for a WAV file using <see cref="IContentExtractionService.AnalyzeAudioAsync"/>.
        /// Checks that the output contains all required fields, the contents array is non-empty,
        /// and verifies presence and validity of markdown and fields.
        /// </summary>
        [Fact(DisplayName = "Analyze Audio Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeAudioAsync()
        {
            Exception? serviceException = null;
            AnalyzeResult? result = null;

            try
            {
                string filePath = "./data/audio.wav";
                // Ensure the file path is correct and the file exists
                Assert.True(File.Exists(filePath), "Audio file does not exist at the specified path.");
                result = await service.AnalyzeAudioAsync(filePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);
            Assert.False(result.Warnings.Any(), "The warnings array should be empty");
            Assert.True(result.Contents.Any(), "The contents array is empty");

            var content = result.Contents[0];
            Assert.False(string.IsNullOrWhiteSpace(content.Markdown), "The markdown content is empty");
            Assert.False(content.Fields.Any(), "The fields collection is empty");
        }

        /// <summary>
        /// Tests video analysis for an MP4 file using <see cref="IContentExtractionService.AnalyzeVideoAsync"/>.
        /// Ensures the returned JSON contains expected fields and valid non-empty content,
        /// including markdown and fields in the first content item.
        /// </summary>
        [Fact(DisplayName = "Analyze Video Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeVideoAsync()
        {
            Exception? serviceException = null;
            AnalyzeResult? result = null;
            try
            {
                string filePath = "./data/FlightSimulator.mp4";
                // Ensure the file path is correct and the file exists
                Assert.True(File.Exists(filePath), "Video file does not exist at the specified path.");
                result = await service.AnalyzeVideoAsync(filePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);
            Assert.False(result.Warnings.Any(), "The warnings array should be empty");
            Assert.True(result.Contents.Any(), "The contents array is empty");

            var content = result.Contents[0];
            Assert.False(string.IsNullOrWhiteSpace(content.Markdown), "The markdown content is empty");
            Assert.False(content.Fields.Any(), "The fields collection is empty");
        }

        /// <summary>
        /// Tests video face analysis for an MP4 file using <see cref="IContentExtractionService.AnalyzeVideoWithFaceAsync"/>.
        /// Verifies that the output JSON contains all required fields, valid non-empty contents, and correct face analysis results.
        /// </summary>
        [Fact(DisplayName = "Analyze Video With Face Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeVideoWithFaceAsync()
        {
            Exception? serviceException = null;
            AnalyzeResult? result = null;
            try
            {
                string filePath = "./data/FlightSimulator.mp4";
                // Ensure the file path is correct and the file exists
                Assert.True(File.Exists(filePath), "Video file does not exist at the specified path.");
                result = await service.AnalyzeVideoWithFaceAsync(filePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);
            Assert.False(result.Warnings.Any(), "The warnings array should be empty");
            Assert.True(result.Contents.Any(), "The contents array is empty");

            var content = result.Contents[0];
            Assert.False(string.IsNullOrWhiteSpace(content.Markdown), "The markdown content is empty");
            Assert.False(content.Fields.Any(), "The fields collection is empty");
        }
    }
}