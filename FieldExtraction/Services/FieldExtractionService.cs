using Azure;
using Azure.AI.ContentUnderstanding;
using ContentUnderstanding.Common;
using FieldExtraction.Interfaces;
using System.Text.Json;

namespace FieldExtraction.Services
{
    public class FieldExtractionService : IFieldExtractionService
    {
        private readonly ContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/field_extraction/";

        public FieldExtractionService(ContentUnderstandingClient client) 
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
        public async Task<AnalyzeResult> CreateAndUseAnalyzer(string analyzerId, ContentAnalyzer analyzer, string fileName)
        {
            Console.WriteLine($"🔧 Creating custom analyzer '{analyzerId}'...");

            // Start the create or replace operation
            var analyzerOperation = await _client.GetContentAnalyzersClient()
                .CreateOrReplaceAsync(
                    waitUntil: WaitUntil.Completed,
                    analyzerId: analyzerId,
                    resource: analyzer);

            // Get the result
            ContentAnalyzer result = analyzerOperation.Value;
            Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");
            Console.WriteLine($"   Status: {result.Status}");
            Console.WriteLine($"   Created At: {result.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   Base Analyzer: {result.BaseAnalyzerId}");
            Console.WriteLine($"   Description: {result.Description}");

            // Display field schema information
            if (result.FieldSchema != null)
            {
                Console.WriteLine($"\n📋 Field Schema: {result.FieldSchema.Name}");
                Console.WriteLine($"   {result.FieldSchema.Description}");
                Console.WriteLine($"   Fields:");
                foreach (var field in result.FieldSchema.Fields)
                {
                    Console.WriteLine($"      - {field.Key}: {field.Value.Type} ({field.Value.Method})");
                    Console.WriteLine($"        {field.Value.Description}");
                }
            }

            // Display any warnings
            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                Console.WriteLine($"\n⚠️  Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"      - {warning.Code}: {warning.Message}");
                }
            }

            // Read file from disk
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"❌ Error: Sample file not found at {fileName}");
                throw new FileNotFoundException("Sample file not found.", fileName);
            }

            byte[] bytes = await File.ReadAllBytesAsync(fileName);
            Console.WriteLine($"\n📄 Analyzing file '{Path.GetFileName(fileName)}'...");

            // Start the analyze operation with binary content
            BinaryData binaryData = BinaryData.FromBytes(bytes);
            var analyzeOperation = await _client.GetContentAnalyzersClient()
                .AnalyzeAsync(
                    waitUntil: WaitUntil.Completed,
                    analyzerId: analyzer.BaseAnalyzerId,
                    data: binaryData);

            // Get the result
            AnalyzeResult analyzeResult = analyzeOperation.Value;

            // Display markdown content
            Console.WriteLine("\n📄 Markdown Content:");
            Console.WriteLine("=" + new string('=', 49));

            // Display document-specific information if available
            PrintExtractionResults(analyzeResult, fileName);

            Console.WriteLine("\n✅ Analysis complete!");

            // Clean up the created analyzer
            Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}'...");
            await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
            Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

            Console.WriteLine("\n💡 Next steps:");
            Console.WriteLine("   - To retrieve an analyzer: see GetAnalyzer sample");
            Console.WriteLine("   - To use the analyzer for analysis: see AnalyzeBinary sample");
            Console.WriteLine("   - To delete an analyzer: see DeleteAnalyzer sample");

            return analyzeResult;
        }

        public void PrintExtractionResults(AnalyzeResult analyzeResult, string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string fileName = Path.GetFileName(filePath);

            Console.WriteLine($"File: {fileName}");
            Console.WriteLine($"Type: {GetFileTypeDescription(extension)}");
            Console.WriteLine($"Analyzer completed at: {DateTime.Now}");
            Console.WriteLine("\nExtracted Fields:");

            Console.WriteLine("\nField Extraction Results:");
            try
            {
                var contents = analyzeResult.Contents;
                var content = contents[0];
                foreach (var field in content.Fields)
                {
                    // PrintFieldValue(field, 0);
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
        //private static void PrintFieldValue(KeyValuePair<string, ContentField> field, int indentLevel)
        //{
        //    string indent = new string(' ', indentLevel * 2);
        //    string fieldType = field.Value.Source ?? "unknown";

        //    try
        //    {
        //        switch (fieldType.ToLower())
        //        {
        //            case "string":
        //            case "number":
        //            case "boolean":
        //            case "date":
        //                var simpleValue = GetSimpleTypeValue(field.Value, fieldType);
        //                Console.WriteLine($"{indent}- {fieldName} ({fieldType}): {simpleValue}");
        //                break;

        //            case "array":
        //                Console.WriteLine($"{indent}- {fieldName} (array):");
        //                if (fieldValue.TryGetProperty("valueArray", out JsonElement arrayValue))
        //                {
        //                    var arrayItems = arrayValue.EnumerateArray().ToArray();
        //                    if (arrayItems.Length == 0)
        //                    {
        //                        Console.WriteLine($"{indent}  [Empty array]");
        //                    }
        //                    else
        //                    {
        //                        for (int i = 0; i < arrayItems.Length; i++)
        //                        {
        //                            Console.WriteLine($"{indent}  Item {i + 1}:");
        //                            PrintFieldValue($"", arrayItems[i], indentLevel + 2);
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    Console.WriteLine($"{indent}  [No array value]");
        //                }
        //                break;

        //            case "object":
        //                Console.WriteLine($"{indent}- {fieldName} (object):");
        //                if (fieldValue.TryGetProperty("valueObject", out JsonElement objectValue))
        //                {
        //                    foreach (var property in objectValue.EnumerateObject())
        //                    {
        //                        PrintFieldValue(property.Name, property.Value, indentLevel + 1);
        //                    }
        //                }
        //                else
        //                {
        //                    Console.WriteLine($"{indent}  [No object value]");
        //                }
        //                break;

        //            default:
        //                Console.WriteLine($"{indent}- {fieldName} ({fieldType}): [Unsupported type]");
        //                break;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"{indent}- {fieldName} ({fieldType}): [Error reading value: {ex.Message}]");
        //        throw;
        //    }
        //}

        /// <summary>
        /// Gets the value for simple types (string, number, boolean, date) based on the pattern "value<Type>".
        /// </summary>
        /// <param name="fieldValue">The JSON element containing the field value and type information</param>
        /// <param name="fieldType">The type of the field</param>
        /// <returns>A string representation of the value or an error message</returns>
        //private static string GetSimpleTypeValue(ContentField fieldValue, string fieldType)
        //{
        //    var typeToPropertyMap = new Dictionary<string, string>
        //    {
        //        { "string", "valueString" },
        //        { "number", "valueNumber" },
        //        { "boolean", "valueBoolean" },
        //        { "date", "valueDate" }
        //    };

        //    string fieldTypeLower = fieldType.ToLower();
        //    if (!typeToPropertyMap.TryGetValue(fieldTypeLower, out string? propertyName))
        //    {
        //        return "[Unknown type]";
        //    }

        //    if (!fieldValue.TryGetProperty(propertyName, out JsonElement valueElement))
        //    {
        //        return "[No value]";
        //    }

        //    try
        //    {
        //        return fieldTypeLower switch
        //        {
        //            "string" => valueElement.GetString() ?? "[Null string]",
        //            "number" => valueElement.GetDecimal().ToString(),
        //            "boolean" => valueElement.GetBoolean().ToString(),
        //            "date" => valueElement.GetString() ?? "[Null date]",
        //            _ => "[Unknown type]"
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return $"[Error reading {fieldType}: {ex.Message}]";
        //    }
        //}

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
