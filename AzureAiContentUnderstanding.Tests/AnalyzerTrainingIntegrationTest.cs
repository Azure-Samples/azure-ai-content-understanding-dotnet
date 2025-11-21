using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    public class AnalyzerTrainingIntegrationTest
    {
        private readonly IAnalyzerTrainingService service;
        // SAS URL for uploading training data to Azure Blob Storage
        // Replace with your SAS URL for actual usage
        private string trainingDataSasUrl = "https://<your-storage-account-name>.blob.core.windows.net/<your-container-name>?<your-sas-token>";
        // Local directory for generated training data (dynamically named for each test run)
        private string trainingDataPath = $"test_training_data_dotnet_{DateTime.Now.ToString("yyyyMMddHHmmss")}/";
        // Local folder containing source documents for training
        private const string trainingDocsFolder = "./data/document_training";

        /// <summary>
        /// Initializes test dependencies and AnalyzerTrainingService via dependency injection.
        /// </summary>
        public AnalyzerTrainingIntegrationTest()
        {
            // Create host and configure services
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();
                }
            );

            service = host.Services.GetService<IAnalyzerTrainingService>()!;
            // Optionally override SAS URL from environment variable
            trainingDataSasUrl = Environment.GetEnvironmentVariable("TRAINING_DATA_SAS_URL") ?? trainingDataSasUrl;
        }

        /// <summary>
        /// Executes the complete analyzer training integration test.
        /// Steps:
        /// 1. Generates training data and uploads to blob storage.
        /// 2. Validates upload completeness.
        /// 3. Creates custom analyzer using template and training data.
        /// 4. Analyzes sample document with custom analyzer and verifies output structure.
        /// </summary>
        [Fact(DisplayName = "Analyzer Training Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            JsonDocument? result = null;
            string analyzerId = $"test_analyzer_training_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            try
            {
                // Step 1: Generate training data and upload to blob storage
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);

                // Step 2: Validate that all local files are uploaded to blob storage
                var files = Directory.GetFiles(trainingDocsFolder, "*.*", SearchOption.AllDirectories).ToList().ToHashSet();

                // Check if the training data is uploaded to the blob storage
                var blobClient = new BlobContainerClient(new Uri(trainingDataSasUrl));
                var blobFiles = new HashSet<string>();

                // Ensure trainingDataPath ends with "/"
                var normalizedPrefix = trainingDataPath.EndsWith("/") ? trainingDataPath : trainingDataPath + "/";

                await foreach (BlobItem blobItem in blobClient.GetBlobsAsync(prefix: normalizedPrefix))
                {
                    var name = blobItem.Name.Substring(normalizedPrefix.Length);
                    if (!string.IsNullOrEmpty(name) && !name.EndsWith("/"))
                    {
                        blobFiles.Add(name);
                    }
                }

                var fileNames = files.Select(f => Path.GetFileName(f)).ToHashSet();

                // Assert: All local files are present in Blob
                Assert.Equal(JsonSerializer.Serialize(fileNames.OrderBy(x => x)),
                             JsonSerializer.Serialize(blobFiles.OrderBy(x => x)));

                // Step 3: Create custom analyzer using training data and template
                // Define the analyzer as a dictionary (matching Python notebook structure)
                var contentAnalyzer = new Dictionary<string, object>
                {
                    ["baseAnalyzerId"] = "prebuilt-document",
                    ["description"] = "Extract useful information from receipt with labeled training data",
                    ["config"] = new Dictionary<string, object>
                    {
                        ["returnDetails"] = true,
                        ["enableLayout"] = true,
                        ["enableFormula"] = false,
                        ["estimateFieldSourceAndConfidence"] = true
                    },
                    ["fieldSchema"] = new Dictionary<string, object>
                    {
                        ["name"] = "receipt schema",
                        ["description"] = "Schema for receipt",
                        ["fields"] = new Dictionary<string, object>
                        {
                            ["MerchantName"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["method"] = "extract",
                                ["description"] = "Name of the merchant"
                            },
                            ["Items"] = new Dictionary<string, object>
                            {
                                ["type"] = "array",
                                ["method"] = "generate",
                                ["description"] = "List of items purchased",
                                ["items"] = new Dictionary<string, object>
                                {
                                    ["type"] = "object",
                                    ["method"] = "extract",
                                    ["description"] = "Individual item details",
                                    ["properties"] = new Dictionary<string, object>
                                    {
                                        ["Quantity"] = new Dictionary<string, object>
                                        {
                                            ["type"] = "string",
                                            ["method"] = "extract",
                                            ["description"] = "Quantity of the item"
                                        },
                                        ["Name"] = new Dictionary<string, object>
                                        {
                                            ["type"] = "string",
                                            ["method"] = "extract",
                                            ["description"] = "Name of the item"
                                        },
                                        ["Price"] = new Dictionary<string, object>
                                        {
                                            ["type"] = "string",
                                            ["method"] = "extract",
                                            ["description"] = "Price of the item"
                                        }
                                    }
                                }
                            },
                            ["TotalPrice"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["method"] = "extract",
                                ["description"] = "Total price on the receipt"
                            }
                        }
                    },
                    ["tags"] = new Dictionary<string, object>
                    {
                        ["demo_type"] = "analyzer_training"
                    },
                    ["models"] = new Dictionary<string, object>
                    {
                        ["completion"] = "gpt-4.1",
                        ["embedding"] = "text-embedding-3-large"  // Required when using knowledge sources
                    }
                };

                // Create analyzer with the loaded definition
                var analyzerResult = await service.CreateAnalyzerAsync(
                    analyzerId,
                    contentAnalyzer,
                    trainingDataSasUrl,
                    trainingDataPath);

                Assert.NotNull(analyzerResult);
                Assert.True(analyzerResult.RootElement.TryGetProperty("status", out var status));

                // Step 4: Analyze sample document with custom analyzer and verify output
                var customAnalyzerSampleFilePath = "./data/receipt.png";
                result = await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);

                Assert.NotNull(result);

                // Verify the result structure
                Assert.True(result.RootElement.TryGetProperty("result", out var resultElement));

                // Check warnings (should be empty or not exist)
                if (resultElement.TryGetProperty("warnings", out var warnings) &&
                    warnings.ValueKind == JsonValueKind.Array)
                {
                    Assert.Empty(warnings.EnumerateArray());
                }

                // Check contents (should exist and not be empty)
                Assert.True(resultElement.TryGetProperty("contents", out var contents));
                Assert.True(contents.ValueKind == JsonValueKind.Array);

                var contentsArray = contents.EnumerateArray().ToList();
                Assert.NotEmpty(contentsArray);

                var content = contentsArray[0];

                // Verify markdown content exists
                Assert.True(content.TryGetProperty("markdown", out var markdown));
                Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()));

                // Verify fields exist
                Assert.True(content.TryGetProperty("fields", out var fields));
                Assert.True(fields.EnumerateObject().Any());
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            finally
            {
                // Cleanup: Delete the analyzer if it was created
                if (!string.IsNullOrEmpty(analyzerId) && serviceException == null)
                {
                    try
                    {
                        await service.DeleteAnalyzerAsync(analyzerId);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }
    }
}
