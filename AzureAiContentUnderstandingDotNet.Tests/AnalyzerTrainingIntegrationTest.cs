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
        private const string trainingDataSasUrl = "SAS_URL"; // Replace with your SAS URL for training data
        private string trainingDataPath = $"test_training_data_dotnet_{DateTime.Now.ToString("yyyyMMddHHmmss")}/"; // Replace with your local path for training data
        private const string trainingDocsFolder = "./data/document_training"; // Folder containing training documents

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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/content_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();
                })
                .Build();

            service = host.Services.GetService<IAnalyzerTrainingService>()!;
        }

        /// <summary>
        /// Tests the <see cref="Service.GenerateTrainingDataOnBlobAsync"/> method to ensure that training data is
        /// correctly  uploaded to blob storage and matches the expected files in the local training documents folder.
        /// </summary>
        /// <remarks>This test verifies that no exceptions are thrown during the execution of the method
        /// and that all files in the  specified training documents folder are successfully uploaded to the blob storage
        /// with the correct structure.</remarks>
        /// <returns></returns>
        [Fact]
        public async Task GenerateTrainingDataOnBlobAsyncTest()
        {
            Exception? serviceException = null;

            try
            {   // Ensure the training documents folder exists
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // no exception should be thrown
            Assert.Null(serviceException);

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

            // Check if all files in the trainingDocsFolder have corresponding label and result files
            Assert.True(files.SetEquals(blobFiles));
        }

        /// <summary>
        /// Tests the functionality of analyzing a document using a custom analyzer.
        /// </summary>
        /// <remarks>This test verifies that a custom analyzer can be created, used to analyze a document,
        /// and subsequently deleted without errors. It ensures that the analysis result contains expected fields such
        /// as "result", "contents", "markdown", and "fields".</remarks>
        /// <returns></returns>
        [Fact]
        public async Task AnalyzeDocumentWithCustomAnalyzerAsyncTest()
        {
            Exception? serviceException = null;
            JsonDocument? resultJson = null;
            var analyzerId = string.Empty;

            try
            {
                var analyzerTemplatePath = "./analyzer_templates/receipt.json";
                analyzerId = await service.CreateAnalyzerAsync(analyzerTemplatePath, trainingDataSasUrl, trainingDataPath);
                var customAnalyzerSampleFilePath = "./data/receipt.png";
                resultJson = await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // no exception should be thrown
            Assert.Null(serviceException);
            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
            Assert.True(result.TryGetProperty("contents", out var contents), "The output JSON lacks the 'contents' field");
            Assert.True(contents.GetArrayLength() > 0, "The contents array is empty");

            var firstContent = contents[0];
            Assert.True(firstContent.TryGetProperty("markdown", out var markdown), "The output content lacks the 'markdown' field");
            Assert.False(string.IsNullOrWhiteSpace(markdown.GetString()), "The markdown content is empty");
            Assert.True(firstContent.TryGetProperty("fields", out var fields), "The output content lacks the 'fields' field");

            try
            {
                // Clean up the analyzer after the test
                if (!string.IsNullOrEmpty(analyzerId))
                {
                    await service.DeleteAnalyzerAsync(analyzerId);
                }
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
