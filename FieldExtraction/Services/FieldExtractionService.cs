using ContentUnderstanding.Common;
using FieldExtraction.Interfaces;
using System.Text.Json;

namespace FieldExtraction.Services
{
    public class FieldExtractionService : IFieldExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/field_extraction/";

        public FieldExtractionService(AzureContentUnderstandingClient client) 
        { 
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Create Analyzer from the Template.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must be non-null and unique  within the system.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template used for creating the analyzer. The path must point to  a valid template
        /// file.</param>
        /// <param name="sampleFilePath">The file path to the sample file to be analyzed. The path must point to a valid file that can  be processed by
        /// the analyzer.</param>
        /// <returns></returns>
        public async Task<JsonDocument> CreateAndUseAnalyzer(string analyzerId, string analyzerTemplatePath, string sampleFilePath)
        {
            Console.WriteLine("Creating Analyzer...");
            Console.WriteLine($"Template: {Path.GetFileName(analyzerTemplatePath)}");
            Console.WriteLine($"Analyzer ID: {analyzerId}");

            // Create analyzer from template
            var createResponse = await _client.BeginCreateAnalyzerAsync(
                analyzerId: analyzerId,
                analyzerTemplatePath: analyzerTemplatePath
            );

            // Poll for creation result
            await _client.PollResultAsync(createResponse);
            Console.WriteLine("\nAnalyzer created successfully");

            Console.WriteLine("\n===== Analyzing Sample File =====");
            Console.WriteLine($"Input file: {Path.GetFileName(sampleFilePath)}");

            // Extract Fields Using the Analyzer.
            // After the analyzer is successfully created, we can use it to analyze our input files.
            var analyzeResponse = await _client.BeginAnalyzeAsync(analyzerId, sampleFilePath);
            JsonDocument resultJson = await _client.PollResultAsync(analyzeResponse);

            Console.WriteLine("\n===== Extraction Results =====");
            PrintExtractionResults(resultJson, sampleFilePath);

            // // Optionally, delete the sample analyzer from your resource. In typical usage scenarios, you would analyze multiple files using the same analyzer.
            Console.WriteLine("\n===== Cleaning Up =====");
            await _client.DeleteAnalyzerAsync(analyzerId);
            Console.WriteLine($"Analyzer {analyzerId} deleted");

            return resultJson;
        }

        public void PrintExtractionResults(JsonDocument resultJson, string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string fileName = Path.GetFileName(filePath);

            Console.WriteLine($"File: {fileName}");
            Console.WriteLine($"Type: {GetFileTypeDescription(extension)}");
            Console.WriteLine($"Analyzer completed at: {DateTime.Now}");
            Console.WriteLine("\nExtracted Fields:");

            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Console.WriteLine("\nField Extraction Results:");
            try
            {
                if (!resultJson.RootElement.TryGetProperty("result", out JsonElement result))
                {
                    Console.WriteLine("No 'result' property found in response.");
                    return;
                }

                if (!result.TryGetProperty("contents", out JsonElement contents))
                {
                    Console.WriteLine("No 'contents' property found in result.");
                    return;
                }

                var contentsArray = contents.EnumerateArray().ToArray();
                if (contentsArray.Length == 0)
                {
                    Console.WriteLine("No content items found.");
                    return;
                }

                var firstContent = contentsArray[0];
                if (!firstContent.TryGetProperty("fields", out JsonElement fields))
                {
                    Console.WriteLine("No fields extracted from the document.");
                    return;
                }

                foreach (var field in fields.EnumerateObject())
                {
                    PrintFieldValue(field.Name, field.Value, 0);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing the result JSON: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error processing results: {ex.Message}");
                return;
            }
        }

        /// <summary>
        /// Prints a field value with proper formatting based on its type.
        /// </summary>
        /// <param name="fieldName">The name of the field</param>
        /// <param name="fieldValue">The JSON element containing the field value and type information</param>
        /// <param name="indentLevel">The indentation level for nested structures</param>
        private static void PrintFieldValue(string fieldName, JsonElement fieldValue, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 2);

            if (!fieldValue.TryGetProperty("type", out JsonElement typeElement))
            {
                Console.WriteLine($"{indent}- {fieldName}: [Unknown type]");
                return;
            }

            string fieldType = typeElement.GetString() ?? "unknown";

            try
            {
                switch (fieldType.ToLower())
                {
                    case "string":
                    case "number":
                    case "boolean":
                    case "date":
                        var simpleValue = GetSimpleTypeValue(fieldValue, fieldType);
                        Console.WriteLine($"{indent}- {fieldName} ({fieldType}): {simpleValue}");
                        break;

                    case "array":
                        Console.WriteLine($"{indent}- {fieldName} (array):");
                        if (fieldValue.TryGetProperty("valueArray", out JsonElement arrayValue))
                        {
                            var arrayItems = arrayValue.EnumerateArray().ToArray();
                            if (arrayItems.Length == 0)
                            {
                                Console.WriteLine($"{indent}  [Empty array]");
                            }
                            else
                            {
                                for (int i = 0; i < arrayItems.Length; i++)
                                {
                                    Console.WriteLine($"{indent}  Item {i + 1}:");
                                    PrintFieldValue($"", arrayItems[i], indentLevel + 2);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{indent}  [No array value]");
                        }
                        break;

                    case "object":
                        Console.WriteLine($"{indent}- {fieldName} (object):");
                        if (fieldValue.TryGetProperty("valueObject", out JsonElement objectValue))
                        {
                            foreach (var property in objectValue.EnumerateObject())
                            {
                                PrintFieldValue(property.Name, property.Value, indentLevel + 1);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{indent}  [No object value]");
                        }
                        break;

                    default:
                        Console.WriteLine($"{indent}- {fieldName} ({fieldType}): [Unsupported type]");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{indent}- {fieldName} ({fieldType}): [Error reading value: {ex.Message}]");
                throw;
            }
        }

        /// <summary>
        /// Gets the value for simple types (string, number, boolean, date) based on the pattern "value<Type>".
        /// </summary>
        /// <param name="fieldValue">The JSON element containing the field value and type information</param>
        /// <param name="fieldType">The type of the field</param>
        /// <returns>A string representation of the value or an error message</returns>
        private static string GetSimpleTypeValue(JsonElement fieldValue, string fieldType)
        {
            var typeToPropertyMap = new Dictionary<string, string>
            {
                { "string", "valueString" },
                { "number", "valueNumber" },
                { "boolean", "valueBoolean" },
                { "date", "valueDate" }
            };

            string fieldTypeLower = fieldType.ToLower();
            if (!typeToPropertyMap.TryGetValue(fieldTypeLower, out string? propertyName))
            {
                return "[Unknown type]";
            }

            if (!fieldValue.TryGetProperty(propertyName, out JsonElement valueElement))
            {
                return "[No value]";
            }

            try
            {
                return fieldTypeLower switch
                {
                    "string" => valueElement.GetString() ?? "[Null string]",
                    "number" => valueElement.GetDecimal().ToString(),
                    "boolean" => valueElement.GetBoolean().ToString(),
                    "date" => valueElement.GetString() ?? "[Null date]",
                    _ => "[Unknown type]"
                };
            }
            catch (Exception ex)
            {
                return $"[Error reading {fieldType}: {ex.Message}]";
            }
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
