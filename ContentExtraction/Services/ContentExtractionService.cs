using Azure;
using ContentExtraction.Interfaces;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Helpers;
using System.Text.Json;

namespace ContentExtraction.Services
{
    public class ContentExtractionService : IContentExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;
        private const string OutputPath = "./sample_output/";

        public ContentExtractionService(AzureContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Resolves the path to a data file, checking multiple locations.
        /// </summary>
        private static string ResolveDataFilePath(string fileName)
        {
            // Try current directory first
            var currentDirPath = Path.Combine("./data", fileName);
            if (File.Exists(currentDirPath))
            {
                return currentDirPath;
            }

            // Try assembly directory (where ContentUnderstanding.Common.dll is located)
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    var assemblyDataPath = Path.Combine(assemblyDir, "data", fileName);
                    if (File.Exists(assemblyDataPath))
                    {
                        return assemblyDataPath;
                    }
                }
            }

            // Try ContentUnderstanding.Common/data relative to current directory
            var commonDataPath = Path.Combine("..", "ContentUnderstanding.Common", "data", fileName);
            if (File.Exists(commonDataPath))
            {
                return commonDataPath;
            }

            // Return the original path if not found (will throw error later)
            return currentDirPath;
        }

        /// <summary>
        /// Analyzes the document at the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the document file to be analyzed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<JsonDocument> AnalyzeDocumentAsync(string filePath)
        {
            // Resolve the file path to handle different execution contexts
            var resolvedPath = ResolveDataFilePath(Path.GetFileName(filePath));
            
            // Check if file exists
            if (!File.Exists(resolvedPath))
            {
                Console.WriteLine($"❌ File '{resolvedPath}' not found.");
                Console.WriteLine("Sample files should be in: ContentUnderstanding.Common/data/");
                Console.WriteLine("   Available sample files: invoice.pdf, mixed_financial_docs.pdf");
                Console.WriteLine("   Please ensure you're running from the correct directory or update the filePath variable.");

                throw new FileNotFoundException($"File '{resolvedPath}' not found.");
            }

            filePath = resolvedPath;

            var analyzerId = "prebuilt-documentSearch";

            Console.WriteLine($"Analyzing local file: {filePath} with {analyzerId}...");

            try
            {
                // Analyze the document
                var response = await _client.BeginAnalyzeBinaryAsync(analyzerId, filePath);
                JsonDocument result = await _client.PollResultAsync(response);

                Console.WriteLine("\nMarkdown Content:");
                Console.WriteLine(new string('=', 50));

                // Extract markdown from the first content element
                var resultProperty = result.RootElement.GetProperty("result");
                var contents = resultProperty.GetProperty("contents");

                JsonElement? content = null;
                string? markdown = null;

                if (contents.GetArrayLength() > 0)
                {
                    content = contents[0];
                    if (content.Value.TryGetProperty("markdown", out var markdownProp))
                    {
                        markdown = markdownProp.GetString();
                        Console.WriteLine(markdown);
                    }
                }
                Console.WriteLine(new string('=', 50));

                // Check if this is document content to access document-specific properties
                if (content.HasValue && content.Value.TryGetProperty("kind", out var kind) && kind.GetString() == "document")
                {
                    var documentContent = content.Value;
                    Console.WriteLine("\nDocument Information:");

                    int startPage = documentContent.GetProperty("startPageNumber").GetInt32();
                    int endPage = documentContent.GetProperty("endPageNumber").GetInt32();

                    Console.WriteLine($"Start page: {startPage}");
                    Console.WriteLine($"End page: {endPage}");
                    Console.WriteLine($"Total pages: {endPage - startPage + 1}");

                    // Check for pages
                    if (documentContent.TryGetProperty("pages", out var pages))
                    {
                        int pageCount = pages.GetArrayLength();
                        Console.WriteLine($"\nPages ({pageCount}):");

                        string unit = documentContent.TryGetProperty("unit", out var unitProp)
                            ? unitProp.GetString() ?? "units"
                            : "units";

                        foreach (var page in pages.EnumerateArray())
                        {
                            int pageNumber = page.GetProperty("pageNumber").GetInt32();
                            double width = page.GetProperty("width").GetDouble();
                            double height = page.GetProperty("height").GetDouble();

                            Console.WriteLine($"  Page {pageNumber}: {width} x {height} {unit}");
                        }
                    }

                    // Check if there are tables in the document
                    if (documentContent.TryGetProperty("tables", out var tables))
                    {
                        int tableCount = tables.GetArrayLength();
                        Console.WriteLine($"\nTables ({tableCount}):");

                        int tableCounter = 1;
                        foreach (var table in tables.EnumerateArray())
                        {
                            int rowCount = table.GetProperty("rowCount").GetInt32();
                            int colCount = table.GetProperty("columnCount").GetInt32();

                            Console.WriteLine($"  Table {tableCounter}: {rowCount} rows x {colCount} columns");
                            tableCounter++;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\nDocument Information: Not available for this content type");
                }

                // Save the result
                string savedJsonPath = SampleHelper.SaveJsonToFile(result, OutputPath, "content_analyzers_analyze_binary");
                Console.WriteLine($"\nFull analysis result saved. Review the complete JSON at: {savedJsonPath}");

                return result;
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine($"\n❌ Request failed:");
                Console.WriteLine($"Status: {ex.Status}");
                Console.WriteLine($"Error Code: {ex.ErrorCode}");
                Console.WriteLine($"Message: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error occurred:");
                Console.WriteLine($"Type: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Analyzes a document from a specified URL using a prebuilt document analyzer.
        /// </summary>
        /// <remarks>This method performs an analysis of a document located at a predefined URL using a
        /// specific analyzer. The analysis extracts content such as markdown, document metadata, pages, and tables, and
        /// outputs the results to the console. The full analysis result is saved as a JSON file for further
        /// review.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<JsonDocument> AnalyzeDocumentFromUrlAsync(string documentUrl)
        {
            // Analyze document from URL
            string analyzerId = "prebuilt-documentSearch";

            Console.WriteLine($"Analyzing document from URL: {documentUrl}");
            Console.WriteLine($"Using analyzer: {analyzerId}\n");

            var response = await _client.BeginAnalyzeUrlAsync(
                analyzerId: analyzerId,
                url: documentUrl
            );

            var result = await _client.PollResultAsync(response);

            Console.WriteLine("\nMarkdown Content:");
            Console.WriteLine(new string('=', 50));

            // Extract markdown from the first content element
            var resultProperty = result.RootElement.GetProperty("result");
            var contents = resultProperty.GetProperty("contents");

            JsonElement? content = null;
            string? markdown = null;

            if (contents.GetArrayLength() > 0)
            {
                content = contents[0];
                if (content.Value.TryGetProperty("markdown", out var markdownProp))
                {
                    markdown = markdownProp.GetString();
                    Console.WriteLine(markdown);
                }
            }
            Console.WriteLine(new string('=', 50));

            // Check if this is document content to access document-specific properties
            if (content.HasValue && content.Value.TryGetProperty("kind", out var kind) && kind.GetString() == "document")
            {
                var documentContent = content.Value;
                Console.WriteLine("\nDocument Information:");

                int startPage = documentContent.GetProperty("startPageNumber").GetInt32();
                int endPage = documentContent.GetProperty("endPageNumber").GetInt32();

                Console.WriteLine($"Start page: {startPage}");
                Console.WriteLine($"End page: {endPage}");
                Console.WriteLine($"Total pages: {endPage - startPage + 1}");

                // Check for pages
                if (documentContent.TryGetProperty("pages", out var pages))
                {
                    int pageCount = pages.GetArrayLength();
                    Console.WriteLine($"\nPages ({pageCount}):");

                    string unit = documentContent.TryGetProperty("unit", out var unitProp)
                        ? unitProp.GetString() ?? "units"
                        : "units";

                    foreach (var page in pages.EnumerateArray())
                    {
                        int pageNumber = page.GetProperty("pageNumber").GetInt32();
                        double width = page.GetProperty("width").GetDouble();
                        double height = page.GetProperty("height").GetDouble();

                        Console.WriteLine($"  Page {pageNumber}: {width} x {height} {unit}");
                    }
                }

                // Check if there are tables in the document
                if (documentContent.TryGetProperty("tables", out var tables))
                {
                    int tableCount = tables.GetArrayLength();
                        Console.WriteLine($"\nTables ({tableCount}):");

                    int tableCounter = 1;
                    foreach (var table in tables.EnumerateArray())
                    {
                        int rowCount = table.GetProperty("rowCount").GetInt32();
                        int colCount = table.GetProperty("columnCount").GetInt32();

                        Console.WriteLine($"  Table {tableCounter}: {rowCount} rows x {colCount} columns");
                        tableCounter++;
                    }
                }
            }
            else
            {
                Console.WriteLine("\nDocument Information: Not available for this content type");
            }

            // Save the result
            string savedJsonPath = SampleHelper.SaveJsonToFile(result, OutputPath, "content_analyzers_url_document");
            Console.WriteLine($"\nFull analysis result saved. Review the complete JSON at: {savedJsonPath}");

            return result;
        }

        /// <summary>
        /// Analyzes an audio file using a prebuilt audio analyzer and processes the results.
        /// </summary>
        /// <remarks>This method performs the following steps: <list type="bullet">
        /// <item><description>Initiates an audio analysis operation using a specified analyzer.</description></item>
        /// <item><description>Waits for the analysis to complete and retrieves the results.</description></item>
        /// <item><description>Processes the analysis results, including extracting markdown content and audio-visual
        /// details such as transcript phrases and timing information.</description></item> </list> The method outputs
        /// relevant information to the console, including a preview of the markdown content, transcript phrases, and
        /// audio-visual metadata.  The full analysis result is saved as a JSON file for further review.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<JsonDocument> AnalyzeAudioAsync(string filePath)
        {
            // Resolve the file path to handle different execution contexts
            var resolvedPath = ResolveDataFilePath(Path.GetFileName(filePath));
            
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Audio file '{resolvedPath}' not found.");
            }

            filePath = resolvedPath;
            string analyzerId = "prebuilt-audio";

            // Analyze audio file with the created analyzer
            Console.WriteLine($"Analyzing audio file from path: {filePath} with analyzer '{analyzerId}'...");

            // Begin audio analysis operation
            Console.WriteLine($"Starting audio analysis with analyzer '{analyzerId}'...");
            var analysisResponse = await _client.BeginAnalyzeBinaryAsync(
                analyzerId: analyzerId,
                fileLocation: filePath
            );

            // Wait for analysis completion
            Console.WriteLine("Waiting for audio analysis to complete...");
            var result = await _client.PollResultAsync(analysisResponse);
            Console.WriteLine("Audio analysis completed successfully!");

            Console.WriteLine("\nMarkdown Content:");
            Console.WriteLine(new string('=', 50));

            // Extract markdown from the first content element
            var resultProperty = result.RootElement.GetProperty("result");
            var contents = resultProperty.GetProperty("contents");

            string? markdown = null;
            JsonElement? content = null;

            if (contents.GetArrayLength() > 0)
            {
                content = contents[0];
                if (content.Value.TryGetProperty("markdown", out var markdownProp))
                {
                    markdown = markdownProp.GetString();
                    Console.WriteLine(markdown);
                }
            }
            Console.WriteLine(new string('=', 50));

            // Check if this is audio-visual content to access audio-visual properties
            if (content.HasValue && content.Value.TryGetProperty("kind", out var kind) && kind.GetString() == "audioVisual")
            {
                var audioVisualContent = content.Value;
                Console.WriteLine("\nAudio-Visual Information:");

                // Basic Audio-Visual Details
                try
                {
                    long startTime = audioVisualContent.GetProperty("startTimeMs").GetInt64();
                    long endTime = audioVisualContent.GetProperty("endTimeMs").GetInt64();
                    double durationSec = (endTime - startTime) / 1000.0;
                    Console.WriteLine($"Start Time: {startTime} ms");
                    Console.WriteLine($"End Time: {endTime} ms");
                    Console.WriteLine($"Duration: {durationSec:F2} seconds");
                }
                catch (Exception)
                {
                    Console.WriteLine("❌ Missing basic audio-visual content details.");
                }

                // Transcript Phrases (limit to 10)
                if (audioVisualContent.TryGetProperty("transcriptPhrases", out var transcriptPhrases))
                {
                    int phrasesCount = transcriptPhrases.GetArrayLength();
                    Console.WriteLine($"\nTranscript Phrases ({Math.Min(phrasesCount, 10)}):");

                    int idx = 0;
                    foreach (var phrase in transcriptPhrases.EnumerateArray().Take(10))
                    {
                        idx++;
                        Console.WriteLine($"  {idx}. Speaker: {phrase.GetProperty("speaker").GetString()}");
                        Console.WriteLine($"     Text: {phrase.GetProperty("text").GetString()}");
                        Console.WriteLine($"     Start: {phrase.GetProperty("startTimeMs").GetInt64()} ms, " +
                                        $"End: {phrase.GetProperty("endTimeMs").GetInt64()} ms");
                        double confidence = phrase.GetProperty("confidence").GetDouble();
                        Console.WriteLine($"     Confidence: {confidence:P2}");
                        Console.WriteLine($"     Locale: {phrase.GetProperty("locale").GetString()}");
                    }

                    if (phrasesCount > 10)
                    {
                        Console.WriteLine($"  ... and {phrasesCount - 10} more.");
                    }
                }
                else
                {
                    Console.WriteLine("\nNo transcript phrases available.");
                }

                // Markdown Preview
                if (!string.IsNullOrEmpty(markdown))
                {
                    Console.WriteLine("\nMarkdown Content Preview:");
                    string preview = markdown.Length > 200 ? markdown.Substring(0, 200) + "..." : markdown;
                    Console.WriteLine(preview);
                }
                else
                {
                    Console.WriteLine("\nNo Markdown content available.");
                }
            }
            else
            {
                Console.WriteLine("\nAudio-Visual Information: Not available for this content type.");
            }

            // Save the result
            string savedJsonPath = SampleHelper.SaveJsonToFile(result, OutputPath, "content_analyzers_audio");
            Console.WriteLine($"\nFull analysis result saved. Review the complete JSON at: {savedJsonPath}");

            return result;
        }

        /// <summary>
        /// Analyzes a video file using a prebuilt video analyzer and processes the analysis results.
        /// </summary>
        /// <remarks>This method performs the following steps: <list type="bullet">
        /// <item><description>Initiates a video analysis operation using a specified analyzer.</description></item>
        /// <item><description>Polls for the completion of the analysis operation.</description></item>
        /// <item><description>Extracts and displays key information, such as markdown content, transcript phrases, and
        /// key frames.</description></item> <item><description>Saves the full analysis result to a JSON file and
        /// processes keyframe images if available.</description></item> </list> The method assumes the video file is
        /// located at a predefined path and uses a specific analyzer ID.</remarks>
        /// <returns></returns>
        public async Task<JsonDocument> AnalyzeVideoAsync(string filePath)
        {
            // Resolve the file path to handle different execution contexts
            var resolvedPath = ResolveDataFilePath(Path.GetFileName(filePath));
            
            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Video file '{resolvedPath}' not found.");
            }

            filePath = resolvedPath;
            string analyzerId = "prebuilt-videoSearch";

            // Analyze video file with the created analyzer
            Console.WriteLine($"Analyzing video file from path: {filePath} with analyzer '{analyzerId}'...");

            // Begin video analysis operation
            Console.WriteLine($"Starting video analysis with analyzer '{analyzerId}'...");
            var analysisResponse = await _client.BeginAnalyzeBinaryAsync(
                analyzerId: analyzerId,
                fileLocation: filePath
            );

            // Wait for analysis completion
            Console.WriteLine("Waiting for video analysis to complete...");
            var result = await _client.PollResultAsync(analysisResponse);
            Console.WriteLine("Video analysis completed successfully!");

            Console.WriteLine("\nMarkdown Content:");
            Console.WriteLine(new string('=', 50));

            // Extract markdown from the first content element
            var resultProperty = result.RootElement.GetProperty("result");
            var contents = resultProperty.GetProperty("contents");

            string? markdown = null;
            JsonElement? content = null;

            if (contents.GetArrayLength() > 0)
            {
                content = contents[0];
                if (content.Value.TryGetProperty("markdown", out var markdownProp))
                {
                    markdown = markdownProp.GetString();
                    Console.WriteLine(markdown);
                }
            }
            Console.WriteLine(new string('=', 50));

            // Check if this is video-visual content to access video-visual properties
            if (content.HasValue && content.Value.TryGetProperty("kind", out var kind) && kind.GetString() == "audioVisual")
            {
                var videoVisualContent = content.Value;
                Console.WriteLine("\nVideo-Visual Information:");

                // Basic Video-Visual Details
                try
                {
                    long startTime = videoVisualContent.GetProperty("startTimeMs").GetInt64();
                    long endTime = videoVisualContent.GetProperty("endTimeMs").GetInt64();
                    double durationSec = (endTime - startTime) / 1000.0;
                    Console.WriteLine($"Start Time: {startTime} ms");
                    Console.WriteLine($"End Time: {endTime} ms");
                    Console.WriteLine($"Duration: {durationSec:F2} seconds");
                }
                catch (Exception)
                {
                    Console.WriteLine("❌ Missing basic audio-visual content details.");
                }

                // Transcript Phrases (limit to 10)
                if (videoVisualContent.TryGetProperty("transcriptPhrases", out var transcriptPhrases))
                {
                    int phrasesCount = transcriptPhrases.GetArrayLength();
                    Console.WriteLine($"\nTranscript Phrases ({Math.Min(phrasesCount, 10)}):");

                    int idx = 0;
                    foreach (var phrase in transcriptPhrases.EnumerateArray().Take(10))
                    {
                        idx++;
                        Console.WriteLine($"  {idx}. Speaker: {phrase.GetProperty("speaker").GetString()}");
                        Console.WriteLine($"     Text: {phrase.GetProperty("text").GetString()}");
                        Console.WriteLine($"     Start: {phrase.GetProperty("startTimeMs").GetInt64()} ms, " +
                                        $"End: {phrase.GetProperty("endTimeMs").GetInt64()} ms");
                        double confidence = phrase.GetProperty("confidence").GetDouble();
                        Console.WriteLine($"     Confidence: {confidence:P2}");
                        Console.WriteLine($"     Locale: {phrase.GetProperty("locale").GetString()}");
                    }

                    if (phrasesCount > 10)
                    {
                        Console.WriteLine($"  ... and {phrasesCount - 10} more.");
                    }
                }
                else
                {
                    Console.WriteLine("\nNo transcript phrases available.");
                }

                // Key Frames (support both keyFrameTimesMs and KeyFrameTimesMs for forward compatibility)
                JsonElement keyFrameTimesMs;
                bool hasKeyFrames = videoVisualContent.TryGetProperty("keyFrameTimesMs", out keyFrameTimesMs) ||
                                   videoVisualContent.TryGetProperty("KeyFrameTimesMs", out keyFrameTimesMs);

                if (hasKeyFrames && keyFrameTimesMs.GetArrayLength() > 0)
                {
                    int keyFrameCount = keyFrameTimesMs.GetArrayLength();
                    Console.WriteLine($"\nKey Frames ({keyFrameCount}):");

                    int idx = 0;
                    foreach (var keyFrameTime in keyFrameTimesMs.EnumerateArray().Take(5))
                    {
                        idx++;
                        Console.WriteLine($"  Frame {idx}: Time {keyFrameTime.GetInt64()} ms");
                    }

                    if (keyFrameCount > 5)
                    {
                        Console.WriteLine($"  ... and {keyFrameCount - 5} more.");
                    }
                }
                else
                {
                    Console.WriteLine("\nNo key frame data available.");
                }

                // Markdown Preview
                if (!string.IsNullOrEmpty(markdown))
                {
                    Console.WriteLine("\nMarkdown Content Preview:");
                    string preview = markdown.Length > 200 ? markdown.Substring(0, 200) + "..." : markdown;
                    Console.WriteLine(preview);
                }
                else
                {
                    Console.WriteLine("\nNo Markdown content available.");
                }
            }
            else
            {
                Console.WriteLine("\nVideo-Visual Information: Not available for this content type.");
            }

            // Save the result
            string savedJsonPath = SampleHelper.SaveJsonToFile(result, OutputPath, "content_analyzers_video");
            Console.WriteLine($"\nFull analysis result saved. Review the complete JSON at: {savedJsonPath}");

            // Keyframe Processing
            var keyframeIds = ExtractKeyframeIds(result);
            if (keyframeIds.Count > 0)
            {
                await DownloadKeyframeImagesAsync(analysisResponse, keyframeIds, analyzerId);
            }
            else
            {
                Console.WriteLine("\n❌ No keyframe IDs found in analysis result.");
            }

            return result;
        }

        /// <summary>
        /// Extracts a list of keyframe IDs from the provided analysis result.
        /// </summary>
        /// <remarks>This method processes the "contents" array within the "result" property of the
        /// provided  <paramref name="analysisResult"/>. It identifies elements of kind "audioVisual" and extracts 
        /// keyframe timestamps from the "keyFrameTimesMs" or "KeyFrameTimesMs" properties. If neither  property is
        /// present, no keyframe IDs are extracted for that element.</remarks>
        /// <param name="analysisResult">A <see cref="JsonDocument"/> representing the analysis result. The document must contain a  "result"
        /// property with a "contents" array, where each element may include keyframe timestamps.</param>
        /// <returns>List of keyframe IDs (e.g., 'keyframes/1000', 'keyframes/2000').</returns>
        private List<string> ExtractKeyframeIds(JsonDocument analysisResult)
        {
            Console.WriteLine("Starting keyframe extraction from analysis result...");
            var keyframeIds = new List<string>();

            var resultProperty = analysisResult.RootElement.GetProperty("result");
            var contents = resultProperty.GetProperty("contents");

            for (int idx = 0; idx < contents.GetArrayLength(); idx++)
            {
                var content = contents[idx];
                if (content.TryGetProperty("kind", out var kind) && kind.GetString() == "audioVisual")
                {
                    Console.WriteLine($"Found audioVisual content at index {idx}:");

                    // Support both keyFrameTimesMs and KeyFrameTimesMs for forward compatibility
                    JsonElement keyFrameTimesMs;
                    bool hasKeyFrames = content.TryGetProperty("keyFrameTimesMs", out keyFrameTimesMs) ||
                                       content.TryGetProperty("KeyFrameTimesMs", out keyFrameTimesMs);

                    if (hasKeyFrames)
                    {
                        int keyFrameCount = keyFrameTimesMs.GetArrayLength();
                        Console.WriteLine($"  Found {keyFrameCount} keyframe timestamps");

                        foreach (var timeMs in keyFrameTimesMs.EnumerateArray())
                        {
                            string keyframeId = $"keyframes/{timeMs.GetInt64()}";
                            keyframeIds.Add(keyframeId);
                        }
                    }
                    else
                    {
                        Console.WriteLine("  No keyframe timestamps found in this audioVisual content.");
                    }
                }
            }

            Console.WriteLine($"Extracted {keyframeIds.Count} total keyframe IDs: {string.Join(", ", keyframeIds)}");
            return keyframeIds;
        }

        /// <summary>
        /// Downloads and saves a subset of keyframe images associated with the specified analysis response.
        /// </summary>
        /// <remarks>This method retrieves the image content for up to three keyframes from the provided
        /// analysis response  and saves each image to a file. If no image content is retrieved for a keyframe, it is
        /// skipped.  The saved file paths are logged to the console.</remarks>
        /// <param name="analysisResponse">The HTTP response message containing the analysis results. This is used to retrieve the keyframe images.</param>
        /// <param name="keyframeIds">A list of keyframe identifiers representing the images to be downloaded. Only the first three keyframes  (or
        /// fewer, if the list contains less than three) will be processed.</param>
        /// <param name="analyzerId">A unique identifier for the analyzer, used to organize and name the saved keyframe image files.</param>
        /// <returns></returns>
        private async Task DownloadKeyframeImagesAsync(
            HttpResponseMessage analysisResponse,
            List<string> keyframeIds,
            string analyzerId)
        {
            Console.WriteLine($"\nDownloading {keyframeIds.Count} keyframe images...");

            var filesToDownload = keyframeIds.Take(Math.Min(3, keyframeIds.Count)).ToList();
            Console.WriteLine($"Files to download (first {filesToDownload.Count}): {string.Join(", ", filesToDownload)}");

            foreach (var keyframeId in filesToDownload)
            {
                Console.WriteLine($"Getting result file: {keyframeId}");

                // Get the result file (keyframe image)
                var imageContent = await _client.GetResultFileAsync(analysisResponse, keyframeId);

                if (imageContent != null)
                {
                    Console.WriteLine($"Retrieved image file for {keyframeId} ({imageContent.Length} bytes)");

                    // Save the image file
                    string savedFilePath = SaveKeyframeImageToFile(
                        imageContent: imageContent,
                        keyframeId: keyframeId,
                        testName: "content_extraction_video",
                        identifier: analyzerId
                    );
                    Console.WriteLine($"Saved keyframe image to: {savedFilePath}");
                }
                else
                {
                    Console.WriteLine($"❌ No image content retrieved for keyframe: {keyframeId}");
                }
            }
        }

        /// <summary>
        /// Saves the provided image content to a file with a generated name based on the keyframe ID, test name, and
        /// optional identifier.
        /// </summary>
        /// <remarks>The method generates a unique file name using the current timestamp, the keyframe ID,
        /// and the test name. If the specified output directory does not exist, it is created automatically.</remarks>
        /// <param name="imageContent">The binary content of the image to be saved.</param>
        /// <param name="keyframeId">The identifier of the keyframe, which may include a path segment. The last segment is used in the file name.</param>
        /// <param name="testName">The name of the test, used as part of the generated file name.</param>
        /// <param name="identifier">An optional identifier to include in the file name to avoid conflicts. If null or empty, it is omitted.</param>
        /// <param name="outputDir">The relative output directory where the file will be saved. Defaults to <see cref="OutputPath"/>.</param>
        /// <returns>The full path of the saved image file.</returns>
        private string SaveKeyframeImageToFile(
            byte[] imageContent,
            string keyframeId,
            string testName,
            string? identifier = null,
            string outputDir = OutputPath)
        {
            // Generate timestamp and frame ID
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Extract the frame time from the keyframe path (e.g., "keyframes/733" -> "733")
            string frameId;
            if (keyframeId.Contains("/"))
            {
                frameId = keyframeId.Split('/').Last();
            }
            else
            {
                // Fallback: use as-is if no slash found
                frameId = keyframeId;
            }

            // Create output directory if it doesn't exist
            string outputDirPath = Path.Combine(Directory.GetCurrentDirectory(), outputDir);
            Directory.CreateDirectory(outputDirPath);

            // Generate output filename with optional identifier to avoid conflicts
            string outputFileName;
            if (!string.IsNullOrEmpty(identifier))
            {
                outputFileName = $"{testName}_{identifier}_{timestamp}_{frameId}.jpg";
            }
            else
            {
                outputFileName = $"{testName}_{timestamp}_{frameId}.jpg";
            }

            string savedFilePath = Path.Combine(outputDirPath, outputFileName);

            // Write the image content to file
            File.WriteAllBytes(savedFilePath, imageContent);

            Console.WriteLine($"Image file saved to: {savedFilePath}");
            return savedFilePath;
        }
    }
}
