using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Text.Json;

namespace AnalyzerTraining
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Create host and configure services
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();
                }
            );

            // Verify client is available
            var client = host.Services.GetService<AzureContentUnderstandingClient>();
            if (client == null)
            {
                Console.WriteLine("❌ Failed to resolve AzureContentUnderstandingClient from DI container.");
                Console.WriteLine("   Please ensure AddContentUnderstandingClient() is called in ConfigureServices.");
                return;
            }

            // Print header
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Azure AI Content Understanding - Analyzer Training Sample");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Print message about ModelDeploymentSetup
            Console.WriteLine("⚠️  IMPORTANT: Before using prebuilt analyzers, you must configure model deployments.");
            Console.WriteLine();
            Console.WriteLine("   If you haven't already, please run the ModelDeploymentSetup sample first:");
            Console.WriteLine("   1. cd ../ModelDeploymentSetup");
            Console.WriteLine("   2. dotnet run");
            Console.WriteLine();
            Console.WriteLine("   This is a one-time setup that maps your deployed GPT models to those required by prebuilt analyzers.");
            Console.WriteLine("   See the main README.md for more details.");
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.Write("Have you already configured model deployments? (y/n): ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            if (answer != "y" && answer != "yes")
            {
                Console.WriteLine();
                Console.WriteLine("Please run the ModelDeploymentSetup sample first and then try again.");
                return;
            }
            Console.WriteLine();

            var service = host.Services.GetRequiredService<IAnalyzerTrainingService>();
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            // Helper to get config value (from appsettings.json first, then environment variable)
            string? GetConfigValue(string key) => configuration.GetValue<string>(key) ?? Environment.GetEnvironmentVariable(key);


            // Get training data configuration (from appsettings.json first, then environment variables)
            string? trainingDataSasUrl = GetConfigValue("TRAINING_DATA_SAS_URL");
            string? trainingDataStorageAccountName = GetConfigValue("TRAINING_DATA_STORAGE_ACCOUNT_NAME");
            string? trainingDataContainerName = GetConfigValue("TRAINING_DATA_CONTAINER_NAME");
            string? trainingDataPath = GetConfigValue("TRAINING_DATA_PATH");

            // Print current configuration values (only print if set)
            bool hasAnyConfig = false;
            
            if (!string.IsNullOrEmpty(trainingDataSasUrl))
            {
                Console.WriteLine($"TRAINING_DATA_SAS_URL: <set>");
                hasAnyConfig = true;
            }
            
            if (!string.IsNullOrEmpty(trainingDataStorageAccountName))
            {
                Console.WriteLine($"TRAINING_DATA_STORAGE_ACCOUNT_NAME: {trainingDataStorageAccountName}");
                hasAnyConfig = true;
            }
            
            if (!string.IsNullOrEmpty(trainingDataContainerName))
            {
                Console.WriteLine($"TRAINING_DATA_CONTAINER_NAME: {trainingDataContainerName}");
                hasAnyConfig = true;
            }
            
            if (!string.IsNullOrEmpty(trainingDataPath))
            {
                Console.WriteLine($"TRAINING_DATA_PATH: {trainingDataPath}");
                hasAnyConfig = true;
            }
            
            if (!hasAnyConfig)
            {
                Console.WriteLine("No training data configuration found in appsettings.json or environment variables.");
            }
            Console.WriteLine();

            // Validate configuration: need either SAS URL OR (storage account + container name)
            bool hasSasUrl = !string.IsNullOrEmpty(trainingDataSasUrl);
            bool hasStorageAccount = !string.IsNullOrEmpty(trainingDataStorageAccountName);
            bool hasContainerName = !string.IsNullOrEmpty(trainingDataContainerName);
            bool hasStorageAccountAndContainer = hasStorageAccount && hasContainerName;

            // Only ask for SAS URL if it's not in appsettings.json AND storage/container names are not there
            if (!hasSasUrl && !hasStorageAccountAndContainer)
            {
                Console.Write("TRAINING_DATA_SAS_URL: Please paste the SAS URL for your Azure Blob container: ");
                trainingDataSasUrl = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(trainingDataSasUrl))
                {
                    // Try to get storage account and container name
                    if (string.IsNullOrEmpty(trainingDataStorageAccountName))
                    {
                        Console.Write("TRAINING_DATA_STORAGE_ACCOUNT_NAME: Please enter the storage account name: ");
                        trainingDataStorageAccountName = Console.ReadLine()?.Trim();
                    }
                    
                    if (string.IsNullOrEmpty(trainingDataContainerName))
                    {
                        Console.Write("TRAINING_DATA_CONTAINER_NAME: Please enter the container name: ");
                        trainingDataContainerName = Console.ReadLine()?.Trim();
                    }
                }
            }

            // Final validation
            hasSasUrl = !string.IsNullOrEmpty(trainingDataSasUrl);
            hasStorageAccountAndContainer = !string.IsNullOrEmpty(trainingDataStorageAccountName) && !string.IsNullOrEmpty(trainingDataContainerName);

            if (!hasSasUrl && !hasStorageAccountAndContainer)
            {
                Console.WriteLine();
                Console.WriteLine("❌ Error: You must provide either:");
                Console.WriteLine("   - TRAINING_DATA_SAS_URL (full SAS URL), OR");
                Console.WriteLine("   - Both TRAINING_DATA_STORAGE_ACCOUNT_NAME and TRAINING_DATA_CONTAINER_NAME");
                Console.WriteLine("   Please configure these in appsettings.json or provide them when prompted.");
                return;
            }

            // Get training data path (only ask if not set in appsettings.json or environment variables)
            if (string.IsNullOrEmpty(trainingDataPath))
            {
                Console.Write("TRAINING_DATA_PATH (press Enter for container root): ");
                trainingDataPath = Console.ReadLine()?.Trim();
            }

            // Normalize path: ensure it ends with / if not empty (empty means container root)
            if (!string.IsNullOrEmpty(trainingDataPath) && !trainingDataPath.EndsWith("/"))
            {
                trainingDataPath += "/";
            }
            // Ensure empty path is explicitly set to empty string (container root)
            if (string.IsNullOrEmpty(trainingDataPath))
            {
                trainingDataPath = "";
            }


            // Prepare training data
            var trainingDocsFolder = ResolveDataFilePath("document_training");
            if (!Directory.Exists(trainingDocsFolder))
            {
                Console.WriteLine($"❌ Error: Training data folder not found at {trainingDocsFolder}");
                Console.WriteLine("   Please ensure the document_training folder exists in the data directory.");
                return;
            }

            // Generate SAS URL from storage account+container if needed
            if (!hasSasUrl && hasStorageAccountAndContainer)
            {
                Console.WriteLine("Generating SAS URL from storage account and container name...");
                try
                {
                    trainingDataSasUrl = await GenerateSasUrlFromStorageAccountAsync(
                        trainingDataStorageAccountName!,
                        trainingDataContainerName!);
                    Console.WriteLine("SAS URL generated successfully.");
                    hasSasUrl = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to generate SAS URL: {ex.Message}");
                    Console.WriteLine("   Please ensure you have proper Azure credentials configured (DefaultAzureCredential).");
                    Console.WriteLine("   Alternatively, provide TRAINING_DATA_SAS_URL directly in appsettings.json.");
                    return;
                }
            }

            // Final check: ensure we have SAS URL for upload
            if (string.IsNullOrEmpty(trainingDataSasUrl))
            {
                Console.WriteLine();
                Console.WriteLine("❌ Error: TRAINING_DATA_SAS_URL is required for upload operations.");
                return;
            }

            Console.WriteLine($"Uploading training data from '{trainingDocsFolder}'...");
            try
            {
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);
                Console.WriteLine("Training data upload completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to upload training data: {ex.Message}");
                return;
            }
            Console.WriteLine();

            // Create analyzer with defined schema
            Console.WriteLine("Creating analyzer with defined schema...");

            // Generate unique analyzer ID (cannot contain hyphens)
            string analyzerId = $"analyzer_training_sample_{Guid.NewGuid().ToString().Replace("-", "_")}";

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

            JsonDocument analyzerResult;
            try
            {
                analyzerResult = await service.CreateAnalyzerAsync(
                    analyzerId,
                    contentAnalyzer,
                    trainingDataSasUrl,
                    trainingDataPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create analyzer: {ex.Message}");
                return;
            }
            Console.WriteLine();

            // Use created analyzer to extract document content
            Console.WriteLine("Analyzing document with custom analyzer...");

            var customAnalyzerSampleFilePath = ResolveDataFilePath("receipt.png");
            if (!File.Exists(customAnalyzerSampleFilePath))
            {
                Console.WriteLine($"⚠️  Warning: Sample file not found at {customAnalyzerSampleFilePath}");
                Console.WriteLine("   Skipping document analysis. The analyzer has been created successfully.");
            }
            else
            {
                try
                {
                    await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to analyze document: {ex.Message}");
                }
            }
            Console.WriteLine();

            // Delete analyzer
            Console.WriteLine("Deleting analyzer...");

            try
            {
                await service.DeleteAnalyzerAsync(analyzerId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete analyzer: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("Analyzer Training Sample Complete!");
        }

        /// <summary>
        /// Resolves the data file path by checking multiple locations.
        /// </summary>
        private static string ResolveDataFilePath(string fileNameOrFolder)
        {
            // Try current directory
            string currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileNameOrFolder);
            if (Directory.Exists(currentDirPath) || File.Exists(currentDirPath))
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
                    string assemblyPath = Path.Combine(assemblyDir, "data", fileNameOrFolder);
                    if (Directory.Exists(assemblyPath) || File.Exists(assemblyPath))
                    {
                        return assemblyPath;
                    }
                }
            }

            // Try ContentUnderstanding.Common/data/
            var commonDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "ContentUnderstanding.Common", "data", fileNameOrFolder);
            if (Directory.Exists(commonDataPath) || File.Exists(commonDataPath))
            {
                return commonDataPath;
            }

            // Try relative to current directory
            if (Directory.Exists(fileNameOrFolder) || File.Exists(fileNameOrFolder))
            {
                return fileNameOrFolder;
            }

            return fileNameOrFolder;
        }

        /// <summary>
        /// Generate a SAS URL for an Azure Blob container using Azure AD authentication.
        /// </summary>
        /// <param name="accountName">The storage account name.</param>
        /// <param name="containerName">The container name.</param>
        /// <returns>The SAS URL for the container.</returns>
        private static async Task<string> GenerateSasUrlFromStorageAccountAsync(string accountName, string containerName)
        {
            var accountUrl = $"https://{accountName}.blob.core.windows.net";
            var credential = new DefaultAzureCredential();
            
            // Create BlobServiceClient with Azure AD authentication
            var blobServiceClient = new BlobServiceClient(new Uri(accountUrl), credential);
            
            // Get user delegation key (valid for 1 hour)
            var startsOn = DateTimeOffset.UtcNow;
            var expiresOn = startsOn.AddHours(1);
            var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(startsOn, expiresOn);
            
            // Generate SAS token with read, write, and list permissions
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                Resource = "c", // Container
                StartsOn = startsOn,
                ExpiresOn = expiresOn
            };
            sasBuilder.SetPermissions(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List);
            
            // Generate SAS token using user delegation key
            var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, accountName).ToString();
            
            // Construct full SAS URL
            return $"{accountUrl}/{containerName}?{sasToken}";
        }
    }
}
