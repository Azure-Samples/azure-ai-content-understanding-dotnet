using Azure;
using Azure.AI.ContentUnderstanding;
using ContentExtraction.Interfaces;

namespace ContentExtraction.Services
{
    public class ContentExtractionService : IContentExtractionService
    {
        private readonly ContentUnderstandingClient _client;
        private const string CacheDir = ".cache";
        private readonly string OutputPath = "./outputs/content_extraction/";

        public ContentExtractionService(ContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(CacheDir))
            {
                Directory.CreateDirectory(CacheDir);
            }

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Analyzes the document at the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the document file to be analyzed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<AnalyzeResult> AnalyzeDocumentAsync(string filePath)
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File '{filePath}' not found.");
                Console.WriteLine("💡 Sample files should be in: ContentUnderstanding.Common/data/");
                Console.WriteLine("   Available sample files: sample_invoice.pdf, mixed_financial_docs.pdf");
                Console.WriteLine("   Please ensure you're running from the correct directory or update the filePath variable.");

                throw new FileNotFoundException($"File '{filePath}' not found.");
            }

            Console.WriteLine($"🔍 Analyzing local file: {filePath} with prebuilt-documentAnalyzer...");

            try
            {
                // Read the pdf file as binary data
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                BinaryData binaryData = new BinaryData(bytes);

                // Analyze the document
                Operation<AnalyzeResult> operation = await _client.GetContentAnalyzersClient()
                    .AnalyzeBinaryAsync(
                        WaitUntil.Completed,
                        "prebuilt-documentAnalyzer",
                        "application/pdf",
                        binaryData);

                AnalyzeResult result = operation.Value;

                // Display the markdown content
                Console.WriteLine("\n📄 Markdown Content:");
                Console.WriteLine("==================================================");

                MediaContent content = result.Contents[0];
                Console.WriteLine(content.Markdown);
                Console.WriteLine("==================================================");

                // Check if this is document content to access document-specific properties
                if (content is DocumentContent documentContent)
                {
                    Console.WriteLine($"\n📚 Document Information:");
                    Console.WriteLine($"Start page: {documentContent.StartPageNumber}");
                    Console.WriteLine($"End page: {documentContent.EndPageNumber}");
                    Console.WriteLine($"Total pages: {documentContent.EndPageNumber - documentContent.StartPageNumber + 1}");

                    // Display page information
                    if (documentContent.Pages != null && documentContent.Pages.Count > 0)
                    {
                        Console.WriteLine($"\n📄 Pages ({documentContent.Pages.Count}):");
                        foreach (var page in documentContent.Pages)
                        {
                            string unit = documentContent.Unit?.ToString() ?? "units";
                            Console.WriteLine($"  Page {page?.PageNumber}: {page?.Width} x {page?.Height} {unit}");
                        }
                    }

                    // Display table information
                    if (documentContent.Tables != null && documentContent.Tables.Count > 0)
                    {
                        Console.WriteLine($"\n📊 Tables ({documentContent.Tables.Count}):");
                        for (int i = 0; i < documentContent.Tables.Count; i++)
                        {
                            var table = documentContent.Tables[i];
                            Console.WriteLine($"  Table {i + 1}: {table.RowCount} rows x {table.ColumnCount} columns");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\n📚 Document Information: Not available for this content type");
                }

                Console.WriteLine("\n✅ Document analysis completed successfully!");

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
        /// Analyzes the audio file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<AnalyzeResult> AnalyzeAudioAsync(string filePath)
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File '{filePath}' not found.");
                Console.WriteLine("💡 Sample files should be in: ContentUnderstanding.Common/data/");
                Console.WriteLine("   Available sample files: mixed_financial_docs.pdf");
                Console.WriteLine("   Please ensure you're running from the correct directory or update the filePath variable.");

                throw new FileNotFoundException($"File '{filePath}' not found.");
            }

            Console.WriteLine($"🔍 Analyzing local file: {filePath} with prebuilt-audioAnalyzer...");

            try
            {
                var audioAnalyzer = new ContentAnalyzer()
                {
                    BaseAnalyzerId = "prebuilt-audioAnalyzer",
                    Config = new ContentAnalyzerConfig
                    {
                        ReturnDetails = true
                    },
                    Description = "Marketing audio analyzer for result file demo",
                    Mode = AnalysisMode.Standard,
                    ProcessingLocation = ProcessingLocation.Global,
                };

                audioAnalyzer.Tags.Add("demo_type", "audio_analysis");

                var analyzerId = $"audio-analyzer-{Guid.NewGuid()}";
                // Start the analyzer creation operation
                Operation<ContentAnalyzer> operation = await _client.GetContentAnalyzersClient().CreateOrReplaceAsync(WaitUntil.Completed, analyzerId: analyzerId, resource: audioAnalyzer).ConfigureAwait(false);

                ContentAnalyzer result = operation.Value;

                Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");
                Console.WriteLine($"   Status: {result.Status}");
                Console.WriteLine($"   Created at: {result.CreatedAt}");

                if (result.Warnings?.Count > 0)
                {
                    Console.WriteLine($"   Warnings: {result.Warnings.Count}");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"     - {warning.Message}");
                    }
                }

                // Read the audio file as binary data
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                BinaryData binaryData = new BinaryData(bytes);

                // Analyze the audio file
                Operation<AnalyzeResult> analyzeOperation = await _client.GetContentAnalyzersClient()
                    .AnalyzeBinaryAsync(
                        waitUntil: WaitUntil.Completed,
                        analyzerId: analyzerId,
                        contentType: "application/octet-stream",
                        input: binaryData);

                AnalyzeResult analyzeResult = analyzeOperation.Value;

                // Clean up: Delete the analyzer (demo cleanup)
                Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}' (demo cleanup)...");
                await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
                Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

                return analyzeResult;
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
        /// Analyzes the video file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<AnalyzeResult> AnalyzeVideoAsync(string filePath)
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File '{filePath}' not found.");
                Console.WriteLine("💡 Sample files should be in: ContentUnderstanding.Common/data/");
                Console.WriteLine("   Available sample files: sample_invoice.pdf, mixed_financial_docs.pdf");
                Console.WriteLine("   Please ensure you're running from the correct directory or update the filePath variable.");

                throw new FileNotFoundException($"File '{filePath}' not found.");
            }

            Console.WriteLine($"🔍 Analyzing local file: {filePath} with prebuilt-documentAnalyzer...");

            try
            {
                var videoAnalyzer = new ContentAnalyzer()
                {
                    BaseAnalyzerId = "prebuilt-videoAnalyzer",
                    Config = new ContentAnalyzerConfig
                    {
                        ReturnDetails = true
                    },
                    Description = "Marketing video analyzer for result file demo",
                    Mode = AnalysisMode.Standard,
                    ProcessingLocation = ProcessingLocation.Global,
                };

                videoAnalyzer.Tags.Add("demo_type", "video_analysis");

                var analyzerId = $"video-analyzer-{Guid.NewGuid()}";
                // Start the analyzer creation operation
                Operation<ContentAnalyzer> operation = await _client.GetContentAnalyzersClient()
                    .CreateOrReplaceAsync(WaitUntil.Completed, analyzerId: analyzerId, resource: videoAnalyzer).ConfigureAwait(false);

                ContentAnalyzer result = operation.Value;

                Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");
                Console.WriteLine($"   Status: {result.Status}");
                Console.WriteLine($"   Created at: {result.CreatedAt}");

                if (result.Warnings?.Count > 0)
                {
                    Console.WriteLine($"   Warnings: {result.Warnings.Count}");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"     - {warning.Message}");
                    }
                }

                // Read the video file as binary data
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                BinaryData binaryData = new BinaryData(bytes);

                // Analyze the video file
                Operation<AnalyzeResult> analyzeOperation = await _client.GetContentAnalyzersClient()
                    .AnalyzeBinaryAsync(
                        waitUntil: WaitUntil.Completed,
                        analyzerId: analyzerId,
                        contentType: "application/octet-stream",
                        input: binaryData);

                var operationId = analyzeOperation.GetRehydrationToken()!.Value.Id;

                AnalyzeResult analyzeResult = analyzeOperation.Value;

                // Look for keyframe times in the analysis result
                var keyframeTimesMs = new List<long>();
                foreach (var content in analyzeResult.Contents)
                {
                    if (content is AudioVisualContent videoContent)
                    {
                        Console.WriteLine($"KeyFrameTimesMs: {string.Join(", ", videoContent.KeyFrameTimesMs ?? new List<long>())}");
                        Console.WriteLine(videoContent);

                        if (videoContent.KeyFrameTimesMs != null)
                        {
                            keyframeTimesMs.AddRange(videoContent.KeyFrameTimesMs);
                        }

                        Console.WriteLine($"📹 Found {keyframeTimesMs.Count} keyframes in video content");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Content is not an AudioVisualContent: {content}");
                    }
                }

                if (keyframeTimesMs.Count == 0)
                {
                    Console.WriteLine("⚠️  No keyframe times found in the analysis result");
                }
                else
                {
                    Console.WriteLine($"🖼️  Found {keyframeTimesMs.Count} keyframe times in milliseconds");
                }

                // Build keyframe filenames using the time values
                List<string> keyframeFiles = keyframeTimesMs
                    .Select(timeMs => $"keyFrame.{timeMs}")
                    .ToList();

                // Download and save a few keyframe images as examples (first, middle, last)
                HashSet<string> framesToDownload;

                if (keyframeFiles.Count >= 3)
                {
                    framesToDownload = new HashSet<string>
                    {
                        keyframeFiles[0],
                        keyframeFiles[keyframeFiles.Count - 1],
                        keyframeFiles[keyframeFiles.Count / 2]
                    };
                }
                else
                {
                    framesToDownload = new HashSet<string>(keyframeFiles);
                }

                List<string> filesToDownload = framesToDownload.ToList();
                Console.WriteLine($"📥 Downloading {filesToDownload.Count} keyframe images as examples: {string.Join(", ", filesToDownload)}");

                foreach (var keyframeId in filesToDownload)
                {
                    Console.WriteLine($"📥 Getting result file: {keyframeId}");

                    // Get the result file (keyframe image)
                    var response = await _client.GetContentAnalyzersClient()
                        .GetResultFileAsync(operationId, keyframeId);
                    var contentType = response.GetRawResponse().Headers.ContentType;
                    var outputFileName = $"{keyframeId}{GetFileExtensionFromContentType(contentType)}";
                    var outputPath = Path.Combine(OutputPath, outputFileName);
                    // Download file
                    await File.WriteAllBytesAsync(outputPath, response.Value.ToArray());
                    Console.WriteLine($"✅ Saved keyframe to: {outputPath} (Content-Type: {contentType ?? "unknown"})");
                }

                // Clean up: Delete the analyzer
                Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}'...");
                await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
                Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

                return analyzeResult;
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
        /// Analyzes the video file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<AnalyzeResult> AnalyzeVideoWithFaceAsync(string filePath)
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File '{filePath}' not found.");
                Console.WriteLine("💡 Sample files should be in: ContentUnderstanding.Common/data/");
                Console.WriteLine("   Available sample files: sample_invoice.pdf, mixed_financial_docs.pdf");
                Console.WriteLine("   Please ensure you're running from the correct directory or update the filePath variable.");

                throw new FileNotFoundException($"File '{filePath}' not found.");
            }

            Console.WriteLine($"🔍 Analyzing local file: {filePath} with prebuilt-documentAnalyzer...");

            try
            {
                var videoAnalyzer = new ContentAnalyzer()
                {
                    BaseAnalyzerId = "prebuilt-videoAnalyzer",
                    Config = new ContentAnalyzerConfig
                    {
                        ReturnDetails = true
                    },
                    Description = "Marketing video analyzer for result file demo",
                    Mode = AnalysisMode.Standard,
                    ProcessingLocation = ProcessingLocation.Global,
                };

                videoAnalyzer.Tags.Add("demo_type", "video_analysis");

                var analyzerId = $"video-analyzer-{Guid.NewGuid()}";
                // Start the analyzer creation operation
                Operation<ContentAnalyzer> operation = await _client.GetContentAnalyzersClient()
                    .CreateOrReplaceAsync(WaitUntil.Completed, analyzerId: analyzerId, resource: videoAnalyzer).ConfigureAwait(false);

                ContentAnalyzer result = operation.Value;

                Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");
                Console.WriteLine($"   Status: {result.Status}");
                Console.WriteLine($"   Created at: {result.CreatedAt}");

                if (result.Warnings?.Count > 0)
                {
                    Console.WriteLine($"   Warnings: {result.Warnings.Count}");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"     - {warning.Message}");
                    }
                }

                // Read the video file as binary data
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                BinaryData binaryData = new BinaryData(bytes);

                // Analyze the video file
                Operation<AnalyzeResult> analyzeOperation = await _client.GetContentAnalyzersClient()
                    .AnalyzeBinaryAsync(
                        waitUntil: WaitUntil.Completed,
                        analyzerId: analyzerId,
                        contentType: "application/octet-stream",
                        input: binaryData);

                var operationId = analyzeOperation.GetRehydrationToken()!.Value.Id;

                AnalyzeResult analyzeResult = analyzeOperation.Value;

                // Look for keyframe times in the analysis result
                var keyframeTimesMs = new List<long>();
                foreach (var content in analyzeResult.Contents)
                {
                    if (content is AudioVisualContent videoContent)
                    {
                        Console.WriteLine($"KeyFrameTimesMs: {string.Join(", ", videoContent.KeyFrameTimesMs ?? new List<long>())}");
                        Console.WriteLine(videoContent);

                        if (videoContent.KeyFrameTimesMs != null)
                        {
                            keyframeTimesMs.AddRange(videoContent.KeyFrameTimesMs);
                        }

                        Console.WriteLine($"📹 Found {keyframeTimesMs.Count} keyframes in video content");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Content is not an AudioVisualContent: {content}");
                    }
                }

                if (keyframeTimesMs.Count == 0)
                {
                    Console.WriteLine("⚠️  No keyframe times found in the analysis result");
                }
                else
                {
                    Console.WriteLine($"🖼️  Found {keyframeTimesMs.Count} keyframe times in milliseconds");
                }

                // Build keyframe filenames using the time values
                List<string> keyframeFiles = keyframeTimesMs
                    .Select(timeMs => $"keyFrame.{timeMs}")
                    .ToList();

                // Download and save a few keyframe images as examples (first, middle, last)
                HashSet<string> framesToDownload;

                if (keyframeFiles.Count >= 3)
                {
                    framesToDownload = new HashSet<string>
                    {
                        keyframeFiles[0],
                        keyframeFiles[keyframeFiles.Count - 1],
                        keyframeFiles[keyframeFiles.Count / 2]
                    };
                }
                else
                {
                    framesToDownload = new HashSet<string>(keyframeFiles);
                }

                List<string> filesToDownload = framesToDownload.ToList();
                Console.WriteLine($"📥 Downloading {filesToDownload.Count} keyframe images as examples: {string.Join(", ", filesToDownload)}");

                foreach (var keyframeId in filesToDownload)
                {
                    Console.WriteLine($"📥 Getting result file: {keyframeId}");

                    // Get the result file (keyframe image)
                    var response = await _client.GetContentAnalyzersClient()
                        .GetResultFileAsync(operationId, keyframeId);
                    var contentType = response.GetRawResponse().Headers.ContentType;
                    var outputFileName = $"{keyframeId}{GetFileExtensionFromContentType(contentType)}";
                    var outputPath = Path.Combine(OutputPath, outputFileName);
                    // Download file
                    await File.WriteAllBytesAsync(outputPath, response.Value.ToArray());
                    Console.WriteLine($"✅ Saved keyframe to: {outputPath} (Content-Type: {contentType ?? "unknown"})");
                }

                // Clean up: Delete the analyzer
                Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}'...");
                await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
                Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

                return analyzeResult;
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
        /// Determines the file extension associated with a given MIME content type.
        /// </summary>
        /// <remarks>This method normalizes the content type by removing any parameters (e.g., charset)
        /// and converting it to lowercase before determining the file extension.</remarks>
        /// <param name="contentType">The MIME content type to evaluate. Can be null or whitespace.</param>
        /// <returns>A string representing the file extension corresponding to the specified content type. Returns ".jpg" if the
        /// content type is null, whitespace, or unrecognized.</returns>
        private string GetFileExtensionFromContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return ".jpg"; // Default extension
            }

            // Normalize the content type (remove any parameters like charset)
            var normalizedContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();

            return normalizedContentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "image/tiff" => ".tiff",
                _ => ".jpg" // Default to jpg for unknown types
            };
        }
    }
}
