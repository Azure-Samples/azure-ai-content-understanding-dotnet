using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Helpers;
using Classifier.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Classifier.Services
{
    public class ClassifierService : IClassifierService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./sample_output/classifier/";

        public ClassifierService(AzureContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Asynchronously classifies a document using an analyzer with contentCategories.
        /// </summary>
        /// <remarks>This method creates an analyzer with contentCategories (which acts as a classifier), 
        /// classifies the specified document, and then deletes the analyzer. Ensure that the file at 
        /// <paramref name="fileLocation"/> exists before calling this method.</remarks>
        /// <param name="classifierId">The unique identifier for the classifier analyzer to be used.</param>
        /// <param name="analyzerDefinition">The analyzer definition dictionary with contentCategories.</param>
        /// <param name="fileLocation">The file path of the document to classify. Must be a valid path to an existing file.</param>
        /// <returns>A <see cref="JsonDocument"/> containing the classification results, or <see langword="null"/> if the file
        /// is not found or an error occurs.</returns>
        public async Task<JsonDocument?> ClassifyDocumentAsync(string classifierId, Dictionary<string, object> analyzerDefinition, string fileLocation)
        {
            try
            {
                // Convert analyzer definition to JSON and save to temp file
                string tempTemplatePath = Path.Combine(Path.GetTempPath(), $"analyzer_{Guid.NewGuid()}.json");
                try
                {
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(tempTemplatePath, JsonSerializer.Serialize(analyzerDefinition, jsonOptions));

                    // Create analyzer with contentCategories (this acts as a classifier)
                    var createResponse = await _client.BeginCreateAnalyzerAsync(
                        analyzerId: classifierId,
                        analyzerTemplatePath: tempTemplatePath);

                    // Poll for analyzer creation completion
                    var classifierResult = await _client.PollResultAsync(createResponse);

                    // Display any warnings
                    if (classifierResult.RootElement.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array)
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
                    string resolvedFilePath = ResolveDataFilePath(fileLocation);
                    if (!File.Exists(resolvedFilePath))
                    {
                        Console.WriteLine($"❌ Error: Sample file not found at {resolvedFilePath}");
                        Console.WriteLine("Sample files should be in: ContentUnderstanding.Common/data/");
                        Console.WriteLine("   Available sample files: mixed_financial_docs.pdf");
                        Console.WriteLine("   Please ensure you're running from the correct directory or update the filePath variable.");
                        return null;
                    }

                    Console.WriteLine($"Classifying document: {Path.GetFileName(resolvedFilePath)}...");

                    // Start the classification operation using analyze binary (same as Python)
                    var analyzeResponse = await _client.BeginAnalyzeBinaryAsync(
                        analyzerId: classifierId,
                        fileLocation: resolvedFilePath);

                    // Poll for classification completion
                    var classificationResult = await _client.PollResultAsync(analyzeResponse);

                    // Display classification results
                    DisplayClassificationResults(classificationResult);

                    // Save result
                    SampleHelper.SaveJsonToFile(classificationResult, OutputPath, $"classification_result_{classifierId}");

                    // Clean up: delete the analyzer
                    await _client.DeleteAnalyzerAsync(classifierId);

                    return classificationResult;
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
            catch (Exception ex)
            {
                Console.WriteLine($"❌ An error occurred: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Creates a custom analyzer for loan applications.
        /// </summary>
        public async Task<string> CreateLoanAnalyzerAsync(string analyzerId)
        {
            try
            {
                // Define custom analyzer as a dictionary
                var customAnalyzer = new Dictionary<string, object>
                {
                    ["baseAnalyzerId"] = "prebuilt-document",
                    ["description"] = "Loan application analyzer - extracts key information from loan applications",
                    ["config"] = new Dictionary<string, object>
                    {
                        ["returnDetails"] = true,
                        ["enableLayout"] = true,
                        ["enableFormula"] = false,
                        ["estimateFieldSourceAndConfidence"] = true
                    },
                    ["fieldSchema"] = new Dictionary<string, object>
                    {
                        ["fields"] = new Dictionary<string, object>
                        {
                            ["ApplicationDate"] = new Dictionary<string, object>
                            {
                                ["type"] = "date",
                                ["method"] = "generate",
                                ["description"] = "The date when the loan application was submitted."
                            },
                            ["ApplicantName"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["method"] = "generate",
                                ["description"] = "Full name of the loan applicant or company."
                            },
                            ["LoanAmountRequested"] = new Dictionary<string, object>
                            {
                                ["type"] = "number",
                                ["method"] = "generate",
                                ["description"] = "The total loan amount requested by the applicant."
                            },
                            ["LoanPurpose"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["method"] = "generate",
                                ["description"] = "The stated purpose or reason for the loan."
                            },
                            ["CreditScore"] = new Dictionary<string, object>
                            {
                                ["type"] = "number",
                                ["method"] = "generate",
                                ["description"] = "Credit score of the applicant, if available."
                            },
                            ["Summary"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["method"] = "generate",
                                ["description"] = "A brief summary overview of the loan application details."
                            }
                        }
                    },
                    ["models"] = new Dictionary<string, string>
                    {
                        ["completion"] = "gpt-4.1"
                    },
                    ["tags"] = new Dictionary<string, string>
                    {
                        ["demo"] = "loan-application"
                    }
                };

                // Convert analyzer definition to JSON and save to temp file
                string tempTemplatePath = Path.Combine(Path.GetTempPath(), $"analyzer_{Guid.NewGuid()}.json");
                try
                {
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(tempTemplatePath, JsonSerializer.Serialize(customAnalyzer, jsonOptions));

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

                    return analyzerId;
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
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create analyzer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a custom analyzer.
        /// </summary>
        public async Task DeleteAnalyzerAsync(string analyzerId)
        {
            try
            {
                await _client.DeleteAnalyzerAsync(analyzerId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete analyzer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Displays classification results in a readable format.
        /// </summary>
        private void DisplayClassificationResults(JsonDocument result)
        {
            if (!result.RootElement.TryGetProperty("result", out var resultProperty))
            {
                Console.WriteLine("No result found in classification response.");
                return;
            }

            if (!resultProperty.TryGetProperty("contents", out var contents) || contents.GetArrayLength() == 0)
            {
                Console.WriteLine("No contents found in classification result.");
                return;
            }

            Console.WriteLine("\nClassification Results:");
            Console.WriteLine(new string('=', 50));

            int segmentIndex = 0;
            foreach (var contentItem in contents.EnumerateArray())
            {
                // Check if this is a segment (from split mode)
                if (contentItem.TryGetProperty("segments", out var segments) && segments.GetArrayLength() > 0)
                {
                    // Display segments
                    foreach (var segment in segments.EnumerateArray())
                    {
                        segmentIndex++;
                        Console.WriteLine($"\nSegment {segmentIndex}:");
                        if (segment.TryGetProperty("category", out var category))
                        {
                            Console.WriteLine($"   Category: {category.GetString()}");
                        }
                        if (segment.TryGetProperty("startPageNumber", out var startPage))
                        {
                            Console.WriteLine($"   Start Page: {startPage.GetInt32()}");
                        }
                        if (segment.TryGetProperty("endPageNumber", out var endPage))
                        {
                            Console.WriteLine($"   End Page: {endPage.GetInt32()}");
                        }

                        // Display extracted fields if available
                        if (segment.TryGetProperty("fields", out var fields))
                        {
                            DisplayExtractedFields(fields);
                        }
                    }
                }
                else
                {
                    // Single document classification
                    segmentIndex++;
                    Console.WriteLine($"\nDocument {segmentIndex}:");
                    if (contentItem.TryGetProperty("category", out var category))
                    {
                        Console.WriteLine($"   Category: {category.GetString()}");
                    }

                    // Display extracted fields if available
                    if (contentItem.TryGetProperty("fields", out var fields))
                    {
                        DisplayExtractedFields(fields);
                    }
                }
            }

            Console.WriteLine(new string('=', 50));
        }

        /// <summary>
        /// Displays extracted fields from a JsonElement.
        /// </summary>
        private void DisplayExtractedFields(JsonElement fields)
        {
            if (fields.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            int fieldCount = 0;
            foreach (var _ in fields.EnumerateObject())
            {
                fieldCount++;
            }

            if (fieldCount == 0)
            {
                return;
            }

            Console.WriteLine($"\n   Extracted Fields ({fieldCount}):");
            foreach (var field in fields.EnumerateObject())
            {
                var fieldName = field.Name;
                var fieldValue = field.Value;

                if (fieldValue.ValueKind == JsonValueKind.Object)
                {
                    if (fieldValue.TryGetProperty("type", out var typeProp))
                    {
                        var fieldType = typeProp.GetString();
                        string? displayValue = null;

                        if (fieldType == "string" && fieldValue.TryGetProperty("valueString", out var valueString))
                        {
                            displayValue = valueString.GetString();
                        }
                        else if (fieldType == "number" && fieldValue.TryGetProperty("valueNumber", out var valueNumber))
                        {
                            displayValue = valueNumber.GetDouble().ToString();
                        }
                        else if (fieldType == "date" && fieldValue.TryGetProperty("valueDate", out var valueDate))
                        {
                            displayValue = valueDate.GetString();
                        }

                        if (displayValue != null)
                        {
                            Console.WriteLine($"      {fieldName}: {displayValue}");
                        }
                    }
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
                "..", "..", "..", "..",
                "ContentUnderstanding.Common", "data", fileName);
            if (File.Exists(commonDataPath))
            {
                return commonDataPath;
            }

            // Try relative to assembly location
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    string commonPath = Path.Combine(assemblyDir, "..", "..", "..", "..", "ContentUnderstanding.Common", "data", fileName);
                    if (File.Exists(commonPath))
                    {
                        return commonPath;
                    }
                }
            }

            // Return original path if not found (will be checked by caller)
            return fileName;
        }
    }
}
