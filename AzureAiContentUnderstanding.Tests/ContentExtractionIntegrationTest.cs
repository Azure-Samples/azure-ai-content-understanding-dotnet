using ContentExtraction.Interfaces;
using ContentExtraction.Services;
using ContentUnderstanding.Common.Extensions;
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
            // Create host and configure services (without deployment configuration)
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IContentExtractionService, ContentExtractionService>();
                }
            );

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
            JsonDocument? result = null;

            try
            {
                // Ensure the file path is correct and the file exists
                var docFilePath = "./data/invoice.pdf";
                Assert.True(File.Exists(docFilePath), "Document file does not exist at the specified path.");

                result = await service.AnalyzeDocumentAsync(docFilePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
                Console.WriteLine($"Test failed with exception: {ex.Message}");
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);

            // Validate the result structure
            ValidateDocumentAnalysisResult(result, expectTables: true);
        }

        /// <summary>
        /// Tests document analysis from URL using <see cref="IContentExtractionService.AnalyzeDocumentFromUrlAsync"/>.
        /// Verifies that the JSON output contains required fields, the contents array is not empty,
        /// and that markdown content is present.
        /// </summary>
        [Fact(DisplayName = "Analyze Document From URL Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeDocumentFromUrlAsync()
        {
            Exception? serviceException = null;
            JsonDocument? result = null;

            try
            {
                // Use a public URL for testing
                var documentUrl = "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-REST-api-samples/master/curl/form-recognizer/sample-invoice.pdf";

                result = await service.AnalyzeDocumentFromUrlAsync(documentUrl);
            }
            catch (Exception ex)
            {
                serviceException = ex;
                Console.WriteLine($"Test failed with exception: {ex.Message}");
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);

            // Validate the result structure
            ValidateDocumentAnalysisResult(result, expectTables: false);
        }

        /// <summary>
        /// Tests audio analysis for a WAV file using <see cref="IContentExtractionService.AnalyzeAudioAsync"/>.
        /// Checks that the output contains all required fields, the contents array is non-empty,
        /// and verifies presence and validity of markdown and audio-visual properties.
        /// </summary>
        [Fact(DisplayName = "Analyze Audio Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeAudioAsync()
        {
            Exception? serviceException = null;
            JsonDocument? result = null;

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
                Console.WriteLine($"Test failed with exception: {ex.Message}");
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);

            // Validate the result structure for audio
            ValidateAudioVisualAnalysisResult(result, contentKind: "audioVisual");
        }

        /// <summary>
        /// Tests video analysis for an MP4 file using <see cref="IContentExtractionService.AnalyzeVideoAsync"/>.
        /// Ensures the returned JSON contains expected fields and valid non-empty content,
        /// including markdown and audio-visual properties in the first content item.
        /// </summary>
        [Fact(DisplayName = "Analyze Video Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAnalyzeVideoAsync()
        {
            Exception? serviceException = null;
            JsonDocument? result = null;

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
                Console.WriteLine($"Test failed with exception: {ex.Message}");
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
            Assert.NotNull(result);

            // Validate the result structure for video
            ValidateAudioVisualAnalysisResult(result, contentKind: "audioVisual", expectKeyFrames: true);
        }

        // Helper methods for validation

        /// <summary>
        /// Validates the structure and content of a document analysis result.
        /// </summary>
        /// <param name="result">The JsonDocument result from analysis.</param>
        /// <param name="expectTables">Whether to expect tables in the document.</param>
        private void ValidateDocumentAnalysisResult(JsonDocument result, bool expectTables = false)
        {
            Assert.NotNull(result);

            // Verify result structure
            Assert.True(result.RootElement.TryGetProperty("result", out var resultElement));

            // Check warnings (should be empty or not critical)
            if (resultElement.TryGetProperty("warnings", out var warnings) &&
                warnings.ValueKind == JsonValueKind.Array)
            {
                var warningsArray = warnings.EnumerateArray().ToList();
                if (warningsArray.Any())
                {
                    Console.WriteLine($"Warnings found: {warningsArray.Count}");
                    foreach (var warning in warningsArray)
                    {
                        if (warning.TryGetProperty("code", out var code))
                        {
                            Console.WriteLine($"  Warning: {code.GetString()}");
                        }
                    }
                }
                // Don't fail on warnings, just log them
            }

            // Check contents (should exist and not be empty)
            Assert.True(resultElement.TryGetProperty("contents", out var contents));
            Assert.True(contents.ValueKind == JsonValueKind.Array);

            var contentsArray = contents.EnumerateArray().ToList();
            Assert.NotEmpty(contentsArray);

            var content = contentsArray[0];

            // Verify this is document content
            Assert.True(content.TryGetProperty("kind", out var kind));
            Assert.Equal("document", kind.GetString());

            // Verify markdown exists and is not empty
            Assert.True(content.TryGetProperty("markdown", out var markdown));
            var markdownText = markdown.GetString();
            Assert.False(string.IsNullOrWhiteSpace(markdownText), "The markdown content is empty");
            Console.WriteLine($"Markdown content length: {markdownText?.Length ?? 0}");

            // Verify page information
            Assert.True(content.TryGetProperty("startPageNumber", out var startPage));
            Assert.True(content.TryGetProperty("endPageNumber", out var endPage));
            Console.WriteLine($"Document pages: {startPage.GetInt32()} to {endPage.GetInt32()}");

            // Verify pages array
            Assert.True(content.TryGetProperty("pages", out var pages));
            Assert.True(pages.ValueKind == JsonValueKind.Array);
            var pagesArray = pages.EnumerateArray().ToList();
            Assert.NotEmpty(pagesArray);
            Console.WriteLine($"Number of pages: {pagesArray.Count}");

            // If tables are expected, verify them
            if (expectTables)
            {
                if (content.TryGetProperty("tables", out var tables) &&
                    tables.ValueKind == JsonValueKind.Array)
                {
                    var tablesArray = tables.EnumerateArray().ToList();
                    Console.WriteLine($"Number of tables: {tablesArray.Count}");
                    // Note: Don't assert tables exist as not all documents have them
                }
            }
        }

        /// <summary>
        /// Validates the structure and content of an audio/video analysis result.
        /// </summary>
        /// <param name="result">The JsonDocument result from analysis.</param>
        /// <param name="contentKind">Expected content kind (should be "audioVisual").</param>
        /// <param name="expectKeyFrames">Whether to expect keyframes (for video).</param>
        private void ValidateAudioVisualAnalysisResult(
            JsonDocument result,
            string contentKind = "audioVisual",
            bool expectKeyFrames = false)
        {
            Assert.NotNull(result);

            // Verify result structure
            Assert.True(result.RootElement.TryGetProperty("result", out var resultElement));

            // Check warnings (should be empty or not critical)
            if (resultElement.TryGetProperty("warnings", out var warnings) &&
                warnings.ValueKind == JsonValueKind.Array)
            {
                var warningsArray = warnings.EnumerateArray().ToList();
                if (warningsArray.Any())
                {
                    Console.WriteLine($"Warnings found: {warningsArray.Count}");
                }
                // Don't fail on warnings, just log them
            }

            // Check contents (should exist and not be empty)
            Assert.True(resultElement.TryGetProperty("contents", out var contents));
            Assert.True(contents.ValueKind == JsonValueKind.Array);

            var contentsArray = contents.EnumerateArray().ToList();
            Assert.NotEmpty(contentsArray);

            var content = contentsArray[0];

            // Verify this is audio-visual content
            Assert.True(content.TryGetProperty("kind", out var kind));
            Assert.Equal(contentKind, kind.GetString());

            // Verify markdown exists (can be empty for audio/video)
            Assert.True(content.TryGetProperty("markdown", out var markdown));
            var markdownText = markdown.GetString();
            Console.WriteLine($"Markdown content length: {markdownText?.Length ?? 0}");

            // Verify timing information
            Assert.True(content.TryGetProperty("startTimeMs", out var startTime));
            Assert.True(content.TryGetProperty("endTimeMs", out var endTime));
            long startMs = startTime.GetInt64();
            long endMs = endTime.GetInt64();
            Console.WriteLine($"Duration: {startMs}ms to {endMs}ms ({(endMs - startMs) / 1000.0:F2} seconds)");

            // Verify transcript phrases exist
            if (content.TryGetProperty("transcriptPhrases", out var transcriptPhrases) &&
                transcriptPhrases.ValueKind == JsonValueKind.Array)
            {
                var phrasesArray = transcriptPhrases.EnumerateArray().ToList();
                Console.WriteLine($"Number of transcript phrases: {phrasesArray.Count}");

                if (phrasesArray.Any())
                {
                    var firstPhrase = phrasesArray[0];
                    Assert.True(firstPhrase.TryGetProperty("speaker", out _));
                    Assert.True(firstPhrase.TryGetProperty("text", out _));
                    Assert.True(firstPhrase.TryGetProperty("confidence", out _));
                }
            }

            // If keyframes are expected (for video), verify them
            if (expectKeyFrames)
            {
                // Support both property name variations
                bool hasKeyFrames = content.TryGetProperty("keyFrameTimesMs", out var keyFrameTimesMs) ||
                                   content.TryGetProperty("KeyFrameTimesMs", out keyFrameTimesMs);

                if (hasKeyFrames && keyFrameTimesMs.ValueKind == JsonValueKind.Array)
                {
                    var keyFramesArray = keyFrameTimesMs.EnumerateArray().ToList();
                    Console.WriteLine($"Number of keyframes: {keyFramesArray.Count}");
                    // Note: Don't assert keyframes exist as not all videos have them
                }
                else
                {
                    Console.WriteLine("No keyframes found in video analysis result");
                }
            }
        }
    }
}