﻿using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    public class AnalyzerTrainingIntegrationTest
    {
        private readonly IAnalyzerTrainingService service;
        // SAS URL for uploading training data to Azure Blob Storage
        // Replace with your SAS URL for actual usage
        private string trainingDataSasUrl = "https://<your_storage_account_name>.blob.core.windows.net/<your_container_name>?<your_sas_token>";
        // Local directory for generated training data (dynamically named for each test run)
        private string trainingDataPath = $"test_training_data_dotnet_{DateTime.Now.ToString("yyyyMMddHHmmss")}/";
        // Local folder containing source documents for training
        private const string trainingDocsFolder = "./data/document_training";

        /// <summary>
        /// Initializes test dependencies and AnalyzerTrainingService via dependency injection.
        /// </summary>
        public AnalyzerTrainingIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Load configuration from environment variables or appsettings.json
                    string? endpoint = Environment.GetEnvironmentVariable("AZURE_CU_CONFIG_Endpoint") ?? context.Configuration.GetValue<string>("AZURE_CU_CONFIG:Endpoint");

                    // API version for Azure Content Understanding service
                    string? apiVersion = Environment.GetEnvironmentVariable("AZURE_CU_CONFIG_ApiVersion") ?? context.Configuration.GetValue<string>("AZURE_CU_CONFIG:ApiVersion");

                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        throw new ArgumentException("Endpoint must be provided in environment variable or appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(apiVersion))
                    {
                        throw new ArgumentException("API version must be provided in environment variable or appsettings.json.");
                    }

                    services.AddConfigurations(opts =>
                    {
                        opts.Endpoint = endpoint;
                        opts.ApiVersion = apiVersion;
                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/analyzer_training";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();
                })
                .Build();

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
            JsonDocument? resultJson = null;
            var analyzerId = string.Empty;

            try
            {   // Step 1: Generate training data and upload to blob storage
                await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);

                // Step 2: Validate that all local files are uploaded to blob storage
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
                // Assert: All local files are present in Blob
                Assert.True(JsonSerializer.Serialize(fileNames) == JsonSerializer.Serialize(blobFiles), "Mismatch between local training data and uploaded blob files");

                // Step 3: Create custom analyzer using training data and template
                var analyzerTemplatePath = "./analyzer_templates/receipt.json";
                analyzerId = await service.CreateAnalyzerAsync(analyzerTemplatePath, trainingDataSasUrl, trainingDataPath);

                // Step 4: Analyze sample document with custom analyzer and verify output
                var customAnalyzerSampleFilePath = "./data/receipt.png";
                resultJson = await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);

                Assert.NotNull(resultJson);
                Assert.True(resultJson.RootElement.TryGetProperty("result", out var result), "The output JSON lacks the 'result' field");
                Assert.True(result.TryGetProperty("warnings", out var warnings));
                Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
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

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }
    }
}
