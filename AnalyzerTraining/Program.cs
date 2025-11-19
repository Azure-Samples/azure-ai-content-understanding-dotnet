using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
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
            Console.WriteLine("# Enhance Your Analyzer with Labeled Data");
            Console.WriteLine();
            Console.WriteLine("> #################################################################################");
            Console.WriteLine(">");
            Console.WriteLine("> Note: Currently, this feature is only available when the analyzer scenario is set to `document`.");
            Console.WriteLine(">");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine();
            Console.WriteLine("Labeled data consists of samples that have been tagged with one or more labels to add context or meaning.");
            Console.WriteLine("This additional information is used to improve the analyzer's performance.");
            Console.WriteLine();
            Console.WriteLine("In your own projects, you can use [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/quickstart/use-ai-foundry)");
            Console.WriteLine("to annotate your data with the labeling tool.");
            Console.WriteLine();
            Console.WriteLine("This sample demonstrates how to create an analyzer using your labeled data and how to analyze your files afterward.");
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Print message about ModelDeploymentSetup
            Console.WriteLine("⚠️  IMPORTANT: Before using prebuilt analyzers, you must configure model deployments.");
            Console.WriteLine();
            Console.WriteLine("   If you haven't already, please run the ModelDeploymentSetup sample first:");
            Console.WriteLine("   1. cd ../ModelDeploymentSetup");
            Console.WriteLine("   2. dotnet run");
            Console.WriteLine();
            Console.WriteLine("   This is a one-time setup that maps your deployed models to prebuilt analyzers.");
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

            // Prerequisites section
            Console.WriteLine("## Prerequisites");
            Console.WriteLine();
            Console.WriteLine("1. Ensure your Azure AI service is configured by following the [configuration steps](../README.md#configure-azure-ai-service-resource) in the main README.");
            Console.WriteLine("2. Set environment variables related to training data by following the steps in [Set env for training data](../docs/set_env_for_training_data_and_reference_doc.md).");
            Console.WriteLine("   - You can either set `TRAINING_DATA_SAS_URL` directly with the SAS URL for your Azure Blob container,");
            Console.WriteLine("   - Or set both `TRAINING_DATA_STORAGE_ACCOUNT_NAME` and `TRAINING_DATA_CONTAINER_NAME` to generate the SAS URL automatically.");
            Console.WriteLine("   - Also set `TRAINING_DATA_PATH` to specify the folder path within the container where the training data will be uploaded.");
            Console.WriteLine();

            // Get training data configuration
            Console.WriteLine("## Prepare Labeled Data");
            Console.WriteLine();
            Console.WriteLine("In this step, we will:");
            Console.WriteLine("- Use the environment variables `TRAINING_DATA_PATH` and SAS URL related variables set in the Prerequisites step.");
            Console.WriteLine("- Attempt to get the SAS URL from the environment variable `TRAINING_DATA_SAS_URL`.");
            Console.WriteLine("- If `TRAINING_DATA_SAS_URL` is not set, try generating it automatically using `TRAINING_DATA_STORAGE_ACCOUNT_NAME` and `TRAINING_DATA_CONTAINER_NAME` environment variables.");
            Console.WriteLine("- Verify that each document file in the local folder has corresponding `.labels.json` and `.result.json` files.");
            Console.WriteLine("- Upload these files to the Azure Blob storage container specified by the environment variables.");
            Console.WriteLine();

            // Get training data SAS URL
            string? trainingDataSasUrl = Environment.GetEnvironmentVariable("TRAINING_DATA_SAS_URL");
            string? trainingDataStorageAccountName = Environment.GetEnvironmentVariable("TRAINING_DATA_STORAGE_ACCOUNT_NAME");
            string? trainingDataContainerName = Environment.GetEnvironmentVariable("TRAINING_DATA_CONTAINER_NAME");
            string? trainingDataPath = Environment.GetEnvironmentVariable("TRAINING_DATA_PATH");

            if (string.IsNullOrEmpty(trainingDataSasUrl))
            {
                if (!string.IsNullOrEmpty(trainingDataStorageAccountName) && !string.IsNullOrEmpty(trainingDataContainerName))
                {
                    Console.WriteLine("⚠️  Note: SAS URL generation from storage account credentials is not yet implemented in the C# thin client.");
                    Console.WriteLine("   Please provide the SAS URL directly via TRAINING_DATA_SAS_URL environment variable or enter it below.");
                    Console.WriteLine();
                }

                Console.Write("TrainingDataSasUrl: Please paste the SAS URL that you have created and hit the [Enter] key: ");
                trainingDataSasUrl = Console.ReadLine()?.Trim();
            }

            if (string.IsNullOrEmpty(trainingDataPath))
            {
                Console.Write("TrainingDataPath: Please write the folder name (e.g., training_data or labeling-data): ");
                trainingDataPath = Console.ReadLine()?.Trim();
            }

            if (string.IsNullOrEmpty(trainingDataSasUrl) || string.IsNullOrEmpty(trainingDataPath))
            {
                Console.WriteLine();
                Console.WriteLine("❌ Error: Both TRAINING_DATA_SAS_URL and TRAINING_DATA_PATH are required.");
                Console.WriteLine("   Please set these environment variables or provide them when prompted.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"📋 Configuration:");
            Console.WriteLine($"   Training Data Path: {trainingDataPath}");
            Console.WriteLine($"   Training Data SAS URL: {(string.IsNullOrEmpty(trainingDataSasUrl) ? "<not set>" : "<set>")}");
            Console.WriteLine();

            if (!trainingDataPath.EndsWith("/"))
            {
                trainingDataPath += "/";
            }

            // Prepare training data
            var trainingDocsFolder = ResolveDataFilePath("document_training");
            if (!Directory.Exists(trainingDocsFolder))
            {
                Console.WriteLine($"❌ Error: Training data folder not found at {trainingDocsFolder}");
                Console.WriteLine("   Please ensure the document_training folder exists in the data directory.");
                return;
            }

            Console.WriteLine($"📤 Uploading training data from '{trainingDocsFolder}'...");
            try
            {
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);
                Console.WriteLine($"✅ Training data upload completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to upload training data: {ex.Message}");
                return;
            }
            Console.WriteLine();

            // Create analyzer with defined schema
            Console.WriteLine("## Create Analyzer with Defined Schema");
            Console.WriteLine();
            Console.WriteLine("Before creating the analyzer, we generate a unique analyzer ID. In this example, we generate a unique suffix");
            Console.WriteLine("so that this sample can be run multiple times to create different analyzers.");
            Console.WriteLine();
            Console.WriteLine("We use **TRAINING_DATA_SAS_URL** and **TRAINING_DATA_PATH** as set in the environment variables and used in the previous step.");
            Console.WriteLine();

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
            Console.WriteLine("## Use Created Analyzer to Extract Document Content");
            Console.WriteLine();
            Console.WriteLine("After the analyzer is successfully created, you can use it to analyze your input files.");
            Console.WriteLine();

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
            Console.WriteLine("## Delete Existing Analyzer in Content Understanding Service");
            Console.WriteLine();
            Console.WriteLine("This snippet is optional and is included to prevent test analyzers from remaining in your service.");
            Console.WriteLine("Without deletion, the analyzer will stay in your service and may be reused in subsequent operations.");
            Console.WriteLine();

            try
            {
                await service.DeleteAnalyzerAsync(analyzerId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to delete analyzer: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Analyzer Training Sample Complete! ===");
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
    }
}
