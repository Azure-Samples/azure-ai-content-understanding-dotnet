using Classifier.Interfaces;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Classifier.Services
{
    public class ClassifierService : IClassifierService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/classifier/";

        public ClassifierService(AzureContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Create a Basic Classifier.
        /// </summary>
        /// <remarks>Create a simple classifier that categorizes documents without additional analysis.</remarks>
        /// <param name="classifierId">The unique identifier for the classifier to be created.</param>
        /// <param name="classifierSchemaPath">The file path to the schema used for creating the classifier. Must be a valid path to a readable file.</param>
        /// <returns></returns>
        public async Task CreateClassifierAsync(string classifierId, string classifierSchemaPath)
        {
            try
            {
                var schema = await File.ReadAllTextAsync(classifierSchemaPath);

                Console.WriteLine("Creating Sample Classifier...");

                Console.WriteLine($"Using schema: {schema}");
                Console.WriteLine($"Classifier ID: {classifierId}");

                var createResponse = await _client.BeginCreateClassifierAsync(classifierId, schema);
                JsonDocument resultJson = await _client.PollResultAsync(createResponse);
                var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

                Console.WriteLine("\nClassifier created successfully:");
                Console.WriteLine(serializedJson);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("already exists"))
                {
                    Console.WriteLine("\n💡 Tip: The classifier already exists. You can:");
                    Console.WriteLine("   1. Use a different classifier ID");
                    Console.WriteLine("   2. Delete the existing classifier first");
                    Console.WriteLine("   3. Skip to document classification");
                }
            }
        }

        /// <summary>
        /// Initiates the classification of a document using a specified classifier.
        /// </summary>
        /// <remarks>Use the classifier to categorize your document.</remarks>
        /// <param name="classifierId">The identifier of the classifier to be used for document classification. Cannot be null or empty.</param>
        /// <param name="fileLocation">The file path of the document to be classified. Must be a valid path to an existing file.</param>
        /// <returns></returns>
        public async Task ClassifyDocumentAsync(string classifierId, string fileLocation)
        {
            try
            {
                Console.WriteLine("Classifying document...");
                Console.WriteLine("Processing... This may take a few minutes for large documents.");
                
                string apiNameDescription = "classifier document";
                var response = await _client.BeginClassifierAsync(classifierId, fileLocation, apiNameDescription);
                JsonDocument? resultJson = await _client.PollResultAsync(response);
                var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

                Console.WriteLine("Classification completed successfully!");

                PrintSections(resultJson);

                Console.WriteLine("\nFull result:");
                Console.WriteLine(serializedJson);

                var output = $"{Path.Combine(OutputPath, $"{nameof(ClassifyDocumentAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
                await File.WriteAllTextAsync(output, serializedJson);
                Console.WriteLine("\n===== Document Classifier has been saved to the following output file path =====");
                Console.WriteLine($"\n{output}\n");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error during document classification: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously creates an enhanced classifier using a custom analyzer.
        /// </summary>
        /// <remarks>This method first creates a custom analyzer using the specified schema and then uses
        /// it to create an enhanced classifier. The enhanced classifier is configured to process different types of
        /// documents with specified analyzers.</remarks>
        /// <param name="analyzerId">The unique identifier for the custom analyzer to be created.</param>
        /// <param name="analyzerSchemaPath">The file path to the schema definition for the custom analyzer.</param>
        /// <param name="enhancedSchemaPath">The file path to the schema definition for the enhanced classifier.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the
        /// created enhanced classifier.</returns>
        public async Task<string> CreateEnhancedClassifierWithCustomAnalyzerAsync(string analyzerId, string analyzerSchemaPath, string enhancedSchemaPath)
        {
            Console.WriteLine("Creating custom analyzer...");
            Console.WriteLine("Analyzer will extract: ");

            var createResponse = await _client.BeginCreateAnalyzerAsync(analyzerId, analyzerSchemaPath);
            JsonDocument resultJson = await _client.PollResultAsync(createResponse);

            Console.WriteLine("Custom analyzer created successfully!");
            Console.WriteLine($"Analyzer ID: {analyzerId}");

            var enhancedClassifierId = $"enhanced-classifier-{Guid.NewGuid()}";

            Console.WriteLine($"Creating enhanced classifier: {enhancedClassifierId}");
            Console.WriteLine($"\nConfiguration: ");
            Console.WriteLine($"  - Loan application documents =====> Custom analyzer with field extraction");
            Console.WriteLine($"  - Invoice documents =====> Prebuilt invoice analyzer");
            Console.WriteLine($"  - Bank_Statement documents =====> Standard processing");

            var enhancedSchemaJson = await File.ReadAllTextAsync(enhancedSchemaPath);
            var enhancedSchemaContent = enhancedSchemaJson.Replace("{analyzerId}", analyzerId);
            var classifierResponse = await _client.BeginCreateClassifierAsync(enhancedClassifierId, enhancedSchemaContent);
            await _client.PollResultAsync(classifierResponse);
            
            Console.WriteLine("\nEnhanced classifier created successfully!");

            return enhancedClassifierId;
        }

        /// <summary>
        /// Processes a document using an enhanced classifier asynchronously.
        /// </summary>
        /// <remarks>Process the document again using our enhanced classifier. Invoices and loan application documents will now have additional fields extracted.</remarks>
        /// <param name="enhancedClassifierId">The identifier of the enhanced classifier to be used for processing.</param>
        /// <param name="fileLocation">The file path of the document to be processed.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ProcessDocumentWithEnhancedClassifierAsync(string enhancedClassifierId, string fileLocation)
        {
            Console.WriteLine("Processing document with enhanced classifier");
            Console.WriteLine($"Document: {fileLocation}");
            Console.WriteLine($"Processing with classification + field extraction...");

            var apiNameDescription = "process document with enhanced classifier";
            var response = await _client.BeginClassifierAsync(enhancedClassifierId, fileLocation, apiNameDescription);
            JsonDocument resultJson = await _client.PollResultAsync(response);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine("Enhanced processing completed!");

            PrintSections(resultJson);

            var output = $"{Path.Combine(OutputPath, $"{nameof(ProcessDocumentWithEnhancedClassifierAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
            await File.WriteAllTextAsync(output, serializedJson);
            Console.WriteLine("\n===== Document With Enhanced Classifier has been saved to the following output file path =====");
            Console.WriteLine($"\n{output}");
        }

        /// <summary>
        /// Prints the sections contained within a JSON document to the console.
        /// </summary>
        /// <remarks>This method extracts and prints the category and page range for each section found in
        /// the JSON document. If a section contains fields, they are serialized and printed as well. If no fields are
        /// present, a message indicating the absence of fields is printed.</remarks>
        /// <param name="resultJson">The JSON document containing the sections to be printed. Must not be <see langword="null"/>.</param>
        /// <param name="title">An optional title to be printed before the sections. Defaults to an empty string if not provided.</param>
        private void PrintSections(JsonDocument resultJson, string title = "")
        {
            if (resultJson != null && resultJson.RootElement.TryGetProperty("result", out JsonElement result) && result.TryGetProperty("contents", out JsonElement contents))
            {
                Console.WriteLine("======================================================================");

                for(int i = 0; i < contents.GetArrayLength(); i++)
                {
                    Console.WriteLine($"{title}");
                    Console.WriteLine("\nSections:");

                    Console.WriteLine($"\nSection {i + 1}: ");
                    Console.WriteLine($"  - Category: {contents[i].GetProperty("category")}");
                    Console.WriteLine($"  - Pages: {contents[i].GetProperty("startPageNumber")} - {contents[i].GetProperty("endPageNumber")}");

                    if (contents[i].TryGetProperty("fields", out JsonElement fields) && fields.ValueKind == JsonValueKind.Object)
                    {
                        Console.WriteLine("\nExtracted Information:");

                        foreach (var field in fields.EnumerateObject())
                        {
                            Console.WriteLine($"- {field.Name}: {JsonSerializer.Serialize(field.Value, new JsonSerializerOptions { WriteIndented = true })}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  - No fields extracted.");
                    }
                }
            }
        }
    }
}
