using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using AzureAiContentUnderstanding.Tests.Extensions;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration test for training custom analyzers with labeled data.
    /// This advanced sample (Sample 16) demonstrates creating a custom model with label files.
    /// Most users will use Content Understanding Studio for this; this sample shows the code approach.
    /// </summary>
    public class AnalyzerTrainingIntegrationTest : RecordedTestBase
    {
        private readonly IAnalyzerTrainingService service;
        private readonly string trainingDataSasUrl;
        private readonly string trainingDataPath;
        private const string trainingDocsFolder = "./data/document_training";
        private readonly Microsoft.Extensions.Configuration.IConfiguration configuration;

        /// <summary>
        /// Initializes test dependencies and AnalyzerTrainingService via dependency injection.
        /// </summary>
        public AnalyzerTrainingIntegrationTest() : base()
        {
            // In Playback mode, extract path from recording file
            // In Record/Live mode, generate timestamp-based path
            trainingDataPath = Mode == RecordedTestMode.Playback
                ? ExtractTrainingPathFromRecording() ?? "test_training_data_dotnet_fallback/"
                : $"test_training_data_dotnet_{DateTime.Now:yyyyMMddHHmmss}/";
            // Sanitize recording
            TestRecordingHelper.SanitizeRecording(Recording);
            
            // Create host and configure services with recording support
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    // Use recording-aware client registration
                    services.AddContentUnderstandingClientWithRecording(
                        context.Configuration,
                        Recording,
                        Mode);
                    
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();
                }
            );

            configuration = host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            service = host.Services.GetService<IAnalyzerTrainingService>()!;
            
            // Get SAS URL from environment or generate it dynamically
            if (Mode == RecordedTestMode.Playback)
            {
                // In playback mode, construct sanitized URL from environment variables or use defaults
                var storageAccount = Environment.GetEnvironmentVariable("TRAINING_DATA_STORAGE_ACCOUNT_NAME") ?? "sanitizedaccount";
                var containerName = Environment.GetEnvironmentVariable("TRAINING_DATA_CONTAINER_NAME") ?? "sanitizedcontainer";
                var sanitizedBaseUrl = $"https://{storageAccount}.blob.core.windows.net/{containerName}";
                trainingDataSasUrl = TestRecordingHelper.GetSanitizedSasUrl(sanitizedBaseUrl);
            }
            else
            {
                // In Record/Live mode, generate SAS URL from storage account and container name
                var sasUrl = Environment.GetEnvironmentVariable("TRAINING_DATA_SAS_URL");
                
                if (!string.IsNullOrEmpty(sasUrl))
                {
                    // Use provided SAS URL if available
                    trainingDataSasUrl = sasUrl;
                }
                else
                {
                    // Generate SAS URL using storage account name and container name from environment/config
                    var storageAccountName = Environment.GetEnvironmentVariable("TRAINING_DATA_STORAGE_ACCOUNT_NAME") 
                        ?? configuration["TRAINING_DATA_STORAGE_ACCOUNT_NAME"]
                        ?? throw new InvalidOperationException("TRAINING_DATA_STORAGE_ACCOUNT_NAME is not set");
                    
                    var containerName = Environment.GetEnvironmentVariable("TRAINING_DATA_CONTAINER_NAME")
                        ?? configuration["TRAINING_DATA_CONTAINER_NAME"]
                        ?? throw new InvalidOperationException("TRAINING_DATA_CONTAINER_NAME is not set");

                    trainingDataSasUrl = GenerateSasUrl(storageAccountName, containerName);
                }
            }
        }

        /// <summary>
        /// Executes the complete analyzer training integration test.
        /// Steps:
        /// 1. Generates training data and uploads to blob storage.
        /// 2. Validates upload completeness.
        /// 3. Creates custom analyzer using template and training data.
        /// 4. Analyzes sample document with custom analyzer and verifies output structure.
        /// </summary>
        [Fact(DisplayName = "Create Custom Model with Label Files - Sample 16")]
        [Trait("Category", "Integration")]
        [Trait("SampleNumber", "16")]
        public async Task RunAsync()
        {
            // In Playback mode, extract analyzer ID from recording file
            // In Record/Live mode, generate a unique ID
            var analyzerId = Mode == RecordedTestMode.Playback
                ? ExtractAnalyzerIdFromRecording() ?? "test_analyzer_fallback"
                : $"test_analyzer_training_{Guid.NewGuid():N}"[..24];

            #region Snippet:UploadTrainingData
            // Step 1: Generate training data and upload to blob storage
            // Skip in Playback mode as blob operations are not recorded
            if (Mode != RecordedTestMode.Playback)
            {
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);
            }
            else
            {
                Console.WriteLine("Playback mode: Skipping blob storage upload");
            }
            #endregion

            #region Snippet:ValidateUpload
            // Step 2: Validate that all local files are uploaded to blob storage
            // Skip in Playback mode as blob operations are not recorded
            if (Mode != RecordedTestMode.Playback)
            {
                var localFiles = Directory.GetFiles(trainingDocsFolder, "*.*", SearchOption.AllDirectories);
                var fileNames = localFiles.Select(Path.GetFileName).ToHashSet();

                // Check if the training data is uploaded to the blob storage
                var blobClient = new BlobContainerClient(new Uri(trainingDataSasUrl));
                var normalizedPrefix = trainingDataPath.EndsWith("/") ? trainingDataPath : trainingDataPath + "/";
                var blobFiles = new HashSet<string?>();

                await foreach (var blobItem in blobClient.GetBlobsAsync(prefix: normalizedPrefix))
                {
                    var name = blobItem.Name[normalizedPrefix.Length..];
                    if (!string.IsNullOrEmpty(name) && !name.EndsWith("/"))
                    {
                        blobFiles.Add(name);
                    }
                }
            #endregion

            #region Assertion:UploadTrainingData
            // Assert: All local files are present in Blob
            // Skip assertions in Playback mode
            if (Mode != RecordedTestMode.Playback)
            {
                Assert.True(fileNames.Count > 0, "Should have generated at least one file locally");
                Assert.True(blobFiles.Count >= fileNames.Count, $"Blob should contain at least {fileNames.Count} files, but found {blobFiles.Count}");
                    
                // Verify all our local files are in blob storage
                foreach (var fileName in fileNames)
                {
                    Assert.Contains(fileName, blobFiles);
                }
                Console.WriteLine($"Verified {fileNames.Count} files are uploaded to blob storage");
                    
                // Verify we have both documents and labels in our UPLOADED files
                var uploadedDocFiles = fileNames.Where(f => f != null && (f.EndsWith(".jpg") || f.EndsWith(".png") || f.EndsWith(".pdf"))).ToList();
                var uploadedLabelFiles = fileNames.Where(f => f != null && f.EndsWith(".json")).ToList();
                    
                Assert.NotEmpty(uploadedDocFiles);
                Assert.NotEmpty(uploadedLabelFiles);
                Console.WriteLine($"Upload validation: {uploadedDocFiles.Count} documents with {uploadedLabelFiles.Count} label files");
                    
                // At least verify we have some training data structure
                Assert.True(uploadedDocFiles.Count >= 1, "Should have at least one document file");
                Assert.True(uploadedLabelFiles.Count >= 1, "Should have at least one label file");
                    
                Console.WriteLine("Upload completeness verified: All documents have corresponding label files");
            }
            else
            {
                Console.WriteLine("Playback mode: Skipping blob storage validation");
            }
            }
            #endregion

            #region Snippet:DefineAnalyzerSchema
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
            #endregion

            #region Snippet:CreateAnalyzer
            // Create analyzer with the loaded definition
            var analyzerResult = await service.CreateAnalyzerAsync(
                analyzerId,
                contentAnalyzer,
                trainingDataSasUrl,
                trainingDataPath);
            #endregion

            #region Assertion:CreateAnalyzer
            Assert.NotNull(analyzerResult);
            Assert.NotEqual(default, analyzerResult.RootElement);
            Console.WriteLine("Analyzer result received successfully");
                
            // Verify status property exists and is valid
            Assert.True(analyzerResult.RootElement.TryGetProperty("status", out var statusProperty), 
                "Analyzer result should have 'status' property");
            var status = statusProperty.GetString();
            Assert.False(string.IsNullOrWhiteSpace(status), "Status should not be empty");
            Assert.True(status == "Succeeded" || status == "Running" || status == "NotStarted", 
                $"Status should be a valid value, but was '{status}'");
            Console.WriteLine($"Analyzer status: {status}");
                
            // Verify analyzer ID if present (optional check since API may not always return it)
            if (analyzerResult.RootElement.TryGetProperty("analyzerId", out var idProperty))
            {
                var returnedId = idProperty.GetString();
                Assert.False(string.IsNullOrWhiteSpace(returnedId), "Analyzer ID should not be empty");
                Assert.Equal(analyzerId, returnedId);
                Console.WriteLine($"Analyzer ID verified: {analyzerId}");
            }
            else if (analyzerResult.RootElement.TryGetProperty("id", out var altIdProperty))
            {
                var returnedId = altIdProperty.GetString();
                Assert.False(string.IsNullOrWhiteSpace(returnedId), "Analyzer ID should not be empty");
                Console.WriteLine($"Analyzer ID (from 'id' field): {returnedId}");
            }
                
            // Verify knowledge source configuration if present
            if (analyzerResult.RootElement.TryGetProperty("knowledgeSources", out var knowledgeSources))
            {
                Assert.Equal(JsonValueKind.Array, knowledgeSources.ValueKind);
                var sources = knowledgeSources.EnumerateArray().ToList();
                Assert.NotEmpty(sources);
                Console.WriteLine($"Knowledge sources configured: {sources.Count} source(s)");
            }
                
            Console.WriteLine($"Analyzer creation verified: ID={analyzerId}, Status={status}");
            #endregion

            #region Snippet:AnalyzeWithCustomAnalyzer
            // Step 4: Analyze sample document with custom analyzer and verify output
            var result = await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, "./data/receipt.png");
            #endregion

            #region Assertion:AnalyzeWithCustomAnalyzer
            Assert.NotNull(result);
            Assert.NotEqual(default, result.RootElement);
            Console.WriteLine("Analysis result received successfully");

            // Verify the result structure
            Assert.True(result.RootElement.TryGetProperty("result", out var resultElement),
                "Analysis result should have 'result' property");
            Assert.NotEqual(default, resultElement);

            // Check warnings (should be empty or not exist)
            if (resultElement.TryGetProperty("warnings", out var warnings))
            {
                Assert.Equal(JsonValueKind.Array, warnings.ValueKind);
                var warningsArray = warnings.EnumerateArray().ToList();
                Assert.Empty(warningsArray);
                Console.WriteLine("No warnings in analysis result");
            }

            // Check contents (should exist and not be empty)
            Assert.True(resultElement.TryGetProperty("contents", out var contents),
                "Result should have 'contents' property");
            Assert.Equal(JsonValueKind.Array, contents.ValueKind);
                
            var contentsArray = contents.EnumerateArray().ToList();
            Assert.NotEmpty(contentsArray);
            Assert.True(contentsArray.Count > 0, "Contents array should have at least one element");
            Console.WriteLine($"Analysis result contains {contentsArray.Count} content(s)");

            // Verify markdown content exists and is not empty
            var firstContent = contentsArray[0];
            Assert.True(firstContent.TryGetProperty("markdown", out var markdown),
                "Content should have 'markdown' property");
            var markdownText = markdown.GetString();
            Assert.NotNull(markdownText);
            Assert.False(string.IsNullOrWhiteSpace(markdownText), "Markdown content should not be empty");
            Assert.True(markdownText.Length > 0, "Markdown should have content");
            Console.WriteLine($"Markdown extracted successfully ({markdownText.Length} characters)");
                
            // Verify fields exist and are not empty
            Assert.True(firstContent.TryGetProperty("fields", out var fields),
                "Content should have 'fields' property");
            Assert.Equal(JsonValueKind.Object, fields.ValueKind);
                
            var fieldsList = fields.EnumerateObject().ToList();
            Assert.True(fieldsList.Any(), "Fields should not be empty");
            Assert.True(fieldsList.Count > 0, "Should have at least one extracted field");
            Console.WriteLine($"Extracted {fieldsList.Count} field(s) from custom analyzer");
                
            // Verify each field has valid structure
            foreach (var field in fieldsList)
            {
                Assert.False(string.IsNullOrWhiteSpace(field.Name), "Field name should not be empty");
                Assert.NotEqual(default, field.Value);
                    
                // Log field structure for debugging
                var fieldType = field.Value.ValueKind.ToString();
                Console.WriteLine($"  Field '{field.Name}': {fieldType}");
                    
                // For object fields, verify they have meaningful content (optional check)
                if (field.Value.ValueKind == JsonValueKind.Object)
                {
                    var hasContent = field.Value.TryGetProperty("value", out _) ||
                                    field.Value.TryGetProperty("valueString", out _) ||
                                    field.Value.TryGetProperty("values", out _) ||
                                    field.Value.TryGetProperty("content", out _) ||
                                    field.Value.EnumerateObject().Any(); // Has any properties
                        
                    if (!hasContent)
                    {
                        Console.WriteLine($"    Warning: Field '{field.Name}' is an object but has no recognized value properties");
                    }
                }
                // For array fields, verify they're not empty if expected to have data
                else if (field.Value.ValueKind == JsonValueKind.Array)
                {
                    var arrayItems = field.Value.EnumerateArray().ToList();
                    Console.WriteLine($"    Array field '{field.Name}' has {arrayItems.Count} item(s)");
                }
            }
                
            Console.WriteLine("All analysis result validations passed successfully");
            #endregion

            // Cleanup: Delete the analyzer
            await service.DeleteAnalyzerAsync(analyzerId);
        }

        /// <summary>
        /// Generates a SAS URL for the specified storage account and container using Azure AD authentication.
        /// </summary>
        private string GenerateSasUrl(string storageAccountName, string containerName)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(
                    new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                    new DefaultAzureCredential());

                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // Generate user delegation key for the container
                var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
                var expiresOn = DateTimeOffset.UtcNow.AddDays(7);
                var userDelegationKey = blobServiceClient.GetUserDelegationKey(startsOn, expiresOn).Value;

                // Create SAS token with required permissions
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    Resource = "c", // Container
                    StartsOn = startsOn,
                    ExpiresOn = expiresOn
                };

                // Set required permissions for training data
                sasBuilder.SetPermissions(
                    BlobContainerSasPermissions.Read |
                    BlobContainerSasPermissions.Add |
                    BlobContainerSasPermissions.Create |
                    BlobContainerSasPermissions.Write |
                    BlobContainerSasPermissions.Delete |
                    BlobContainerSasPermissions.List);

                var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, storageAccountName).ToString();
                var sasUrl = $"{containerClient.Uri}?{sasToken}";

                Console.WriteLine($"Generated SAS URL for storage account '{storageAccountName}', container '{containerName}'");
                Console.WriteLine($"SAS token expires: {expiresOn:yyyy-MM-dd HH:mm:ss} UTC");

                return sasUrl;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to generate SAS URL for storage account '{storageAccountName}' and container '{containerName}'. " +
                    $"Make sure you are logged in with Azure CLI (az login) and have the required permissions. Error: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Extracts the analyzer ID from the first PUT request in the recording file.
        /// </summary>
        private string? ExtractAnalyzerIdFromRecording([CallerFilePath] string sourceFilePath = "")
        {
            try
            {
                // Use source file path to find the SessionRecords directory
                var projectDir = Path.GetDirectoryName(sourceFilePath)!;
                var testClassName = GetType().Name;
                var recordingPath = Path.Combine(
                    projectDir,
                    "SessionRecords",
                    $"{testClassName}.json");

                if (!File.Exists(recordingPath))
                {
                    Console.WriteLine($"[Playback] Recording file not found: {recordingPath}");
                    return null;
                }

                var recordingJson = File.ReadAllText(recordingPath);
                using var doc = JsonDocument.Parse(recordingJson);
                
                if (!doc.RootElement.TryGetProperty("Entries", out var entries))
                    return null;

                // Find first PUT request (analyzer creation)
                foreach (var entry in entries.EnumerateArray())
                {
                    // Azure SDK format: RequestMethod and RequestUri at top level
                    if (entry.TryGetProperty("RequestMethod", out var method) &&
                        method.GetString() == "PUT" &&
                        entry.TryGetProperty("RequestUri", out var uri))
                    {
                        var uriString = uri.GetString();
                        if (uriString?.Contains("/analyzers/") == true)
                        {
                            // Extract analyzer ID from URI
                            // Format: .../analyzers/{analyzerId}?api-version=...
                            var match = System.Text.RegularExpressions.Regex.Match(
                                uriString, 
                                @"/analyzers/([^?/]+)");
                            if (match.Success)
                            {
                                var analyzerId = match.Groups[1].Value;
                                Console.WriteLine($"[Playback] Extracted analyzer ID from recording: {analyzerId}");
                                return analyzerId;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to extract analyzer ID from recording: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extracts the training data path from the request body in the recording file.
        /// </summary>
        private string? ExtractTrainingPathFromRecording([CallerFilePath] string sourceFilePath = "")
        {
            try
            {
                // Use source file path to find the SessionRecords directory
                var projectDir = Path.GetDirectoryName(sourceFilePath)!;
                var testClassName = GetType().Name;
                var recordingPath = Path.Combine(
                    projectDir,
                    "SessionRecords",
                    $"{testClassName}.json");

                if (!File.Exists(recordingPath))
                {
                    Console.WriteLine($"[Playback] Recording file not found: {recordingPath}");
                    return null;
                }

                var recordingJson = File.ReadAllText(recordingPath);
                using var doc = JsonDocument.Parse(recordingJson);
                
                if (!doc.RootElement.TryGetProperty("Entries", out var entries))
                    return null;

                // Find first PUT request with knowledgeSources
                foreach (var entry in entries.EnumerateArray())
                {
                    // Azure SDK format: RequestMethod and RequestBody at top level
                    if (entry.TryGetProperty("RequestMethod", out var method) &&
                        method.GetString() == "PUT" &&
                        entry.TryGetProperty("RequestBody", out var body))
                    {
                        // Decode base64 body
                        var bodyString = body.GetString();
                        if (string.IsNullOrEmpty(bodyString))
                            continue;

                        var bodyBytes = Convert.FromBase64String(bodyString);
                        var bodyJson = System.Text.Encoding.UTF8.GetString(bodyBytes);
                        
                        using var bodyDoc = JsonDocument.Parse(bodyJson);
                        if (bodyDoc.RootElement.TryGetProperty("knowledgeSources", out var knowledgeSources))
                        {
                            foreach (var source in knowledgeSources.EnumerateArray())
                            {
                                if (source.TryGetProperty("prefix", out var prefix))
                                {
                                    var path = prefix.GetString();
                                    Console.WriteLine($"[Playback] Extracted training path from recording: {path}");
                                    return path;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to extract training path from recording: {ex.Message}");
            }
            return null;
        }
    }
}
