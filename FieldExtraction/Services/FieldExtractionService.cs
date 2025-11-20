using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Helpers;
using FieldExtraction.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FieldExtraction.Services
{
    public class FieldExtractionService : IFieldExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./sample_output/field_extraction/";

        public FieldExtractionService(AzureContentUnderstandingClient client) 
        { 
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Analyze a file using a prebuilt analyzer.
        /// </summary>
        public async Task<JsonDocument> AnalyzeWithPrebuiltAnalyzer(string prebuiltAnalyzerId, string fileName, string filenamePrefix)
        {
            // Resolve file path
            string resolvedFilePath = ResolveDataFilePath(fileName);
            if (!File.Exists(resolvedFilePath))
            {
                Console.WriteLine($"❌ Error: Sample file not found at {resolvedFilePath}");
                throw new FileNotFoundException("Sample file not found.", resolvedFilePath);
            }

            Console.WriteLine($"Sample file: {resolvedFilePath}");

            try
            {
                // Analyze the file
                var analyzeResponse = await _client.BeginAnalyzeBinaryAsync(prebuiltAnalyzerId, resolvedFilePath);
                var analysisResult = await _client.PollResultAsync(analyzeResponse);

                // Display extracted fields (shows how to navigate the result)
                DisplayExtractedFields(analysisResult, isPrebuilt: true);

                // Save result
                SampleHelper.SaveJsonToFile(analysisResult, OutputPath, filenamePrefix);

                return analysisResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Analysis failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Display extracted fields from the analysis result.
        /// Shows how to navigate and extract values from the fields structure.
        /// </summary>
        private void DisplayExtractedFields(JsonDocument result, bool isPrebuilt = false)
        {
            if (!result.RootElement.TryGetProperty("result", out var resultProperty))
                return;

            if (!resultProperty.TryGetProperty("contents", out var contents) || contents.GetArrayLength() == 0)
            {
                Console.WriteLine("No content found in analysis result.");
                return;
            }

            var firstContent = contents[0];
            if (!firstContent.TryGetProperty("fields", out var fields))
            {
                Console.WriteLine("No fields extracted.");
                return;
            }

            int fieldCount = 0;
            foreach (var _ in fields.EnumerateObject())
            {
                fieldCount++;
            }

            // Show partial list message if there are many fields (only for prebuilt)
            if (isPrebuilt && fieldCount > 10)
            {
                Console.WriteLine($"\nExtracted Fields (showing first 10 of {fieldCount} - see output file for complete results):");
            }
            else
            {
                Console.WriteLine("\nExtracted Fields:");
            }
            Console.WriteLine("-".PadRight(80, '-'));

            int displayedCount = 0;
            int maxDisplay = isPrebuilt ? 10 : int.MaxValue; // Show all fields for custom analyzers

            foreach (var field in fields.EnumerateObject())
            {
                if (displayedCount >= maxDisplay)
                    break;

                var fieldValue = field.Value;
                if (!fieldValue.TryGetProperty("type", out var fieldType))
                    continue;

                var typeStr = fieldType.GetString();
                switch (typeStr)
                {
                    case "string":
                        if (fieldValue.TryGetProperty("valueString", out var valueString))
                        {
                            Console.WriteLine($"{field.Name}: {valueString.GetString()}");
                        }
                        break;
                    case "number":
                        if (fieldValue.TryGetProperty("valueNumber", out var valueNumber))
                        {
                            Console.WriteLine($"{field.Name}: {valueNumber.GetDouble()}");
                        }
                        break;
                    case "date":
                        if (fieldValue.TryGetProperty("valueDate", out var valueDate))
                        {
                            Console.WriteLine($"{field.Name}: {valueDate.GetString()}");
                        }
                        break;
                    case "array":
                        if (fieldValue.TryGetProperty("valueArray", out var valueArray))
                        {
                            Console.WriteLine($"{field.Name} (array with {valueArray.GetArrayLength()} items):");
                            int idx = 1;
                            foreach (var item in valueArray.EnumerateArray())
                            {
                                if (item.TryGetProperty("type", out var itemType))
                                {
                                    var itemTypeStr = itemType.GetString();
                                    if (itemTypeStr == "object")
                                    {
                                        Console.WriteLine($"  Item {idx}:");
                                        if (item.TryGetProperty("valueObject", out var valueObject))
                                        {
                                            foreach (var objField in valueObject.EnumerateObject())
                                            {
                                                if (objField.Value.TryGetProperty("type", out var objType))
                                                {
                                                    var objTypeStr = objType.GetString();
                                                    if (objTypeStr == "string" && objField.Value.TryGetProperty("valueString", out var objString))
                                                    {
                                                        Console.WriteLine($"    {objField.Name}: {objString.GetString()}");
                                                    }
                                                    else if (objTypeStr == "number" && objField.Value.TryGetProperty("valueNumber", out var objNumber))
                                                    {
                                                        Console.WriteLine($"    {objField.Name}: {objNumber.GetDouble()}");
                                                    }
                                                }
                                                // Display confidence and source for nested fields (especially for prebuilt)
                                                if (isPrebuilt)
                                                {
                                                    if (objField.Value.TryGetProperty("confidence", out var objConfidence))
                                                    {
                                                        Console.WriteLine($"      Confidence: {objConfidence.GetDouble():F3}");
                                                    }
                                                    if (objField.Value.TryGetProperty("source", out var objSource))
                                                    {
                                                        Console.WriteLine($"      Bounding Box: {objSource}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (itemTypeStr == "string" && item.TryGetProperty("valueString", out var itemString))
                                    {
                                        Console.WriteLine($"  {idx}. {itemString.GetString()}");
                                    }
                                    else if (itemTypeStr == "number" && item.TryGetProperty("valueNumber", out var itemNumber))
                                    {
                                        Console.WriteLine($"  {idx}. {itemNumber.GetDouble()}");
                                    }
                                    else if (itemTypeStr == "date" && item.TryGetProperty("valueDate", out var itemDate))
                                    {
                                        Console.WriteLine($"  {idx}. {itemDate.GetString()}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  {idx}. {item}");
                                    }
                                }
                                idx++;
                            }
                        }
                        break;
                    case "object":
                        if (fieldValue.TryGetProperty("valueObject", out var fieldValueObject))
                        {
                            Console.WriteLine($"{field.Name}: {fieldValueObject}");
                        }
                        break;
                }

                // Display confidence and source if available (especially important for prebuilt analyzers)
                if (isPrebuilt)
                {
                    if (fieldValue.TryGetProperty("confidence", out var confidence))
                    {
                        Console.WriteLine($"  Confidence: {confidence.GetDouble():F3}");
                    }
                    if (fieldValue.TryGetProperty("source", out var source))
                    {
                        Console.WriteLine($"  Bounding Box: {source}");
                    }
                }
                Console.WriteLine();
                displayedCount++;
            }

            if (isPrebuilt && fieldCount > maxDisplay)
            {
                Console.WriteLine($"... and {fieldCount - maxDisplay} more fields. See output file for complete results.");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Create Analyzer and use it to analyze a file.
        /// </summary>
        public async Task<JsonDocument> CreateAndUseAnalyzer(string analyzerId, Dictionary<string, object> analyzerDefinition, string fileName)
        {
            // Convert analyzer definition to JSON and save to temp file
            string tempTemplatePath = Path.Combine(Path.GetTempPath(), $"analyzer_{Guid.NewGuid()}.json");
            try
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(tempTemplatePath, JsonSerializer.Serialize(analyzerDefinition, jsonOptions));

                // Create analyzer using thin client
                var createResponse = await _client.BeginCreateAnalyzerAsync(
                    analyzerId: analyzerId,
                    analyzerTemplatePath: tempTemplatePath);

                // Poll for analyzer creation completion
                var analyzerResult = await _client.PollResultAsync(createResponse);

                // Display any warnings
                if (analyzerResult.RootElement.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
                {
                    if (warnings.GetArrayLength() > 0)
                    {
                        Console.WriteLine("⚠️  Warnings:");
                        foreach (var warning in warnings.EnumerateArray())
                        {
                            var code = warning.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : "";
                            var message = warning.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "";
                            Console.WriteLine($"   - {code}: {message}");
                        }
                    }
                }

                // Resolve file path
                string resolvedFilePath = ResolveDataFilePath(fileName);
                if (!File.Exists(resolvedFilePath))
                {
                    Console.WriteLine($"❌ Error: Sample file not found at {resolvedFilePath}");
                    throw new FileNotFoundException("Sample file not found.", resolvedFilePath);
                }

                Console.WriteLine($"Sample file: {resolvedFilePath}");

                // Analyze the file
                var analyzeResponse = await _client.BeginAnalyzeBinaryAsync(analyzerId, resolvedFilePath);
                var analysisResult = await _client.PollResultAsync(analyzeResponse);

                // Display extracted fields (shows how to navigate the result)
                DisplayExtractedFields(analysisResult, isPrebuilt: false);

                // Save result
                SampleHelper.SaveJsonToFile(analysisResult, OutputPath, $"field_extraction_{analyzerId}");

                // Clean up the created analyzer
                await _client.DeleteAnalyzerAsync(analyzerId);

                return analysisResult;
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempTemplatePath))
                {
                    File.Delete(tempTemplatePath);
                }
            }
        }

        /// <summary>
        /// Resolves the data file path by checking multiple locations.
        /// </summary>
        private static string ResolveDataFilePath(string fileName)
        {
            // Try current directory
            string currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);
            if (File.Exists(currentDirPath))
            {
                return currentDirPath;
            }

            // Try assembly directory (output directory)
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string assemblyPath = Path.Combine(assemblyDir, "data", fileName);
                    if (File.Exists(assemblyPath))
                    {
                        return assemblyPath;
                    }
                }
            }

            // Try ContentUnderstanding.Common/data/
            var commonDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "ContentUnderstanding.Common", "data", fileName);
            if (File.Exists(commonDataPath))
            {
                return commonDataPath;
            }

            // Try as-is (absolute path or relative to current directory)
            if (File.Exists(fileName))
            {
                return fileName;
            }

            // If not found, return the original path (will throw FileNotFoundException later)
            return fileName;
        }

        public string GetFileTypeDescription(string extension) => extension switch
        {
            ".pdf" => "PDF Document",
            ".mp3" => "Audio Recording",
            ".wav" => "Audio Recording",
            ".mp4" => "Video File",
            ".mov" => "Video File",
            _ => "Unknown File Type"
        };
    }
}
