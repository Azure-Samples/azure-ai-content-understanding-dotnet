using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstandingDotNet.Tests
{
    public class AnalyzerTrainingIntegrationTest
    {
        private readonly IAnalyzerTrainingService service;
        // Replace with your SAS URL for training data
        private const string trainingDataSasUrl = "SAS_URL";
        // Replace with your local path for training data
        private string trainingDataPath = $"test_training_data_dotnet_{DateTime.Now.ToString("yyyyMMddHHmmss")}/";
        // Folder containing training documents
        private const string trainingDocsFolder = "./data/document_training"; 

        public AnalyzerTrainingIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint")))
                    {
                        throw new ArgumentException("Endpoint must be provided in appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion")))
                    {
                        throw new ArgumentException("API version must be provided in appsettings.json.");
                    }
                    services.AddConfigurations(opts =>
                    {
                        context.Configuration.GetSection("AZURE_CU_CONFIG").Bind(opts);
                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/analyzer_training";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();
                })
                .Build();

            service = host.Services.GetService<IAnalyzerTrainingService>()!;
        }

        /// <summary>
        /// Executes an integration test for the analyzer training process, verifying the generation of training data,
        /// the creation of a custom analyzer, and the analysis of a sample document using the custom analyzer.
        /// </summary>
        /// <remarks>This test performs the following steps: 1. Generates training data and uploads it to
        /// blob storage. 2. Validates that the training data files are correctly uploaded. 3. Creates a custom analyzer
        /// using a predefined template and the uploaded training data. 4. Analyzes a sample document using the custom
        /// analyzer and verifies the structure and content of the result.  The test ensures that no exceptions are
        /// thrown during the process and validates the correctness of the output.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task RunAsync()
        {
            Exception? serviceException = null;
            JsonDocument? resultJson = null;
            var analyzerId = string.Empty;

            try
            {   // Ensure the training documents folder exists
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);

                var files = Directory.GetFiles(trainingDocsFolder, "*.*", SearchOption.AllDirectories).ToList().ToHashSet();
                // check if the training data is uploaded to the blob storage
                var blobClient = new BlobContainerClient(new Uri(trainingDataSasUrl));
                var blobFiles = new HashSet<string>();
                await foreach (BlobItem blobItem in blobClient.GetBlobsAsync(prefix: trainingDataPath))
                {
                    var name = blobItem.Name.Substring(trainingDataPath.Length);
                    if (!string.IsNullOrEmpty(name) && !name.EndsWith("/"))
                    {
                        blobFiles.Add(name);
                    }
                }

                var fileNames = files.Select(f => Path.GetRelativePath(trainingDocsFolder, f)).ToHashSet();
                // Check if all files in the trainingDocsFolder have corresponding label and result files
                Assert.True(JsonSerializer.Serialize(fileNames) == JsonSerializer.Serialize(blobFiles));

                var analyzerTemplatePath = "./analyzer_templates/receipt.json";
                analyzerId = await service.CreateAnalyzerAsync(analyzerTemplatePath, trainingDataSasUrl, trainingDataPath);
                var customAnalyzerSampleFilePath = "./data/receipt.png";
                resultJson = await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);

                Assert.NotNull(resultJson);
                Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
                Assert.True(result.TryGetProperty("contents", out var contents), "The output JSON lacks the 'contents' field");
                Assert.True(contents.GetArrayLength() > 0, "The contents array is empty");

                var firstContent = contents[0];
                Assert.True(firstContent.TryGetProperty("markdown", out var markdown), "The output content lacks the 'markdown' field");
                Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()), "The markdown content is empty");
                Assert.True(firstContent.TryGetProperty("fields", out var fields), "The output content lacks the 'fields' field");
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // no exception should be thrown
            Assert.Null(serviceException);
        }
    }
}
