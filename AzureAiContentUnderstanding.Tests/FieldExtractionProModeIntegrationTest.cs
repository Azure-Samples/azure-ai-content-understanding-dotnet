using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtractionProMode.Interfaces;
using FieldExtractionProMode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration tests for Field Extraction Pro Mode using IFieldExtractionProModeService.
    /// Verifies analyzer creation, reference document handling, and document analysis for
    /// advanced field extraction scenarios, including bonus cases.
    /// </summary>
    public class FieldExtractionProModeIntegrationTest
    {
        private readonly IFieldExtractionProModeService service;
        private readonly AzureContentUnderstandingClient client;
        // SAS URL for the Azure Blob Storage container to upload training data
        private string accountName = "";
        private string containerName = "";

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CONTENT_UNDERSTANDING_ENDPOINT" or "AZURE_APIVERSION" are missing.
        /// </exception>
        public FieldExtractionProModeIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Load configuration from environment variables or appsettings.json
                    string? endpoint = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") ?? context.Configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_ENDPOINT");

                    // API version for Azure Content Understanding service
                    string? apiVersion = Environment.GetEnvironmentVariable("AZURE_APIVERSION") ?? context.Configuration.GetValue<string>("AZURE_APIVERSION");

                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        throw new ArgumentException("Endpoint must be provided in environment variable or appsettings.json.");
                    }
                    if (string.IsNullOrWhiteSpace(apiVersion))
                    {
                        throw new ArgumentException("API version must be provided in environment variable or appsettings.json.");
                    }

                    // account name
                    accountName = Environment.GetEnvironmentVariable("REFERENCE_DOC_STORAGE_ACCOUNT_NAME") ?? context.Configuration.GetValue<string>("REFERENCE_DOC_STORAGE_ACCOUNT_NAME") ?? "";

                    // container name
                    containerName = Environment.GetEnvironmentVariable("REFERENCE_DOC_CONTAINER_NAME") ?? context.Configuration.GetValue<string>("REFERENCE_DOC_CONTAINER_NAME") ?? "";

                    if (string.IsNullOrWhiteSpace(accountName))
                    {
                        throw new ArgumentException("Storage account name must be provided in environment variable or appsettings.json.");
                    }

                    if (string.IsNullOrWhiteSpace(containerName))
                    {
                        throw new ArgumentException("Storage container name must be provided in environment variable or appsettings.json.");
                    }

                    services.AddConfigurations(opts =>
                    {
                        opts.Endpoint = endpoint;
                        opts.ApiVersion = apiVersion;
                        opts.SubscriptionKey = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_KEY") ?? context.Configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_KEY") ?? "";

                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/field_extraction_pro_mode";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IFieldExtractionProModeService, FieldExtractionProModeService>();
                })
                .Build();

            service = host.Services.GetService<IFieldExtractionProModeService>()!;
            client = host.Services.GetService<AzureContentUnderstandingClient>()!;
        }

        /// <summary>
        /// Runs the Field Extraction Pro Mode integration workflow.
        /// Covers creation and validation of analyzers with schemas, reference document upload,
        /// document analysis, and cleanup. Also runs bonus scenarios for insurance claims.
        /// </summary>
        [Fact(DisplayName = "Field Extraction Pro Mode Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                var referenceDocPath = $"test_field_extraction_pro_mode_data_dotnet_{DateTime.Now.ToString("yyyyMMddHHmmss")}/";
                var referenceDocsFolder = "./data/field_extraction_pro_mode/invoice_contract_verification/reference_docs";
                var analyzer_template = "./analyzer_templates/invoice_contract_verification_pro_mode.json";
                var input_docs = "./data/field_extraction_pro_mode/invoice_contract_verification/input_docs";
                // Construct the SAS URL for the blob storage container
                var referenceDocSasUrl = await service.GetReferenceContainerSasUrlAsync(accountName, containerName);

                // Validate input files and templates exist
                Assert.True(Directory.GetFiles(referenceDocsFolder).Length > 0);
                Assert.True(File.Exists(analyzer_template));
                Assert.True(Directory.GetFiles(input_docs).Length > 0);

                var analyzerId = $"pro-mode-sample-{Guid.NewGuid()}";

                // Main scenario: create analyzer and analyze documents
                await CreateAnalyzerWithDefinedSchemaForProModeAsync(
                    analyzerId,
                    referenceDocsFolder: referenceDocsFolder,
                    referenceDocSasUrl: referenceDocSasUrl,
                    referenceDocPath: referenceDocPath,
                    analyzer_template: analyzer_template,
                    input_docs: input_docs,
                    skipAnalyze: false);

                await AnalyzeDocumentWithDefinedSchemaForProModeAsync(
                    analyzerId: analyzerId,
                    input_docs: input_docs,
                    skipAnalyze: false);

                // Cleanup analyzer
                await service.DeleteAnalyzerAsync(analyzerId);

                // Bonus scenario: insurance claims
                var analyzer_template_for_bonus_sample = "./analyzer_templates/insurance_claims_review_pro_mode.json";
                var input_docs_for_bonus_sample = "./data/field_extraction_pro_mode/insurance_claims_review/input_docs";
                var reference_docs_for_bonus_sample = "./data/field_extraction_pro_mode/insurance_claims_review/reference_docs";
                var analyzer_id_for_bonus_sample = $"pro-mode-sample-bonus-{Guid.NewGuid()}";

                // Validate bonus sample files and run workflow
                await CreateAnalyzerWithDefinedSchemaForProModeAsync(
                    analyzerId: analyzer_id_for_bonus_sample,
                    referenceDocsFolder: reference_docs_for_bonus_sample,
                    referenceDocSasUrl: referenceDocSasUrl,
                    referenceDocPath: referenceDocPath,
                    analyzer_template: analyzer_template_for_bonus_sample,
                    input_docs: input_docs_for_bonus_sample,
                    skipAnalyze: true);

                await AnalyzeDocumentWithDefinedSchemaForProModeAsync(
                    analyzerId: analyzer_id_for_bonus_sample,
                    input_docs: input_docs_for_bonus_sample,
                    skipAnalyze: true);

                await service.DeleteAnalyzerAsync(analyzer_id_for_bonus_sample);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Creates an analyzer using a defined schema in Pro Mode, uploading reference documents to blob storage
        /// and validating that all expected files are present in the container.
        /// </summary>
        /// <param name="analyzerId">Unique identifier for the analyzer.</param>
        /// <param name="referenceDocsFolder">Local folder containing reference documents.</param>
        /// <param name="referenceDocSasUrl">SAS URL for the blob storage container.</param>
        /// <param name="referenceDocPath">Blob storage path prefix for the uploaded reference documents.</param>
        /// <param name="analyzer_template">Path to the analyzer schema template (JSON).</param>
        /// <param name="input_docs">Location of input documents for analysis.</param>
        /// <param name="skipAnalyze">If true, skips some analysis validation checks (used for bonus scenarios).</param>
        private async Task CreateAnalyzerWithDefinedSchemaForProModeAsync(
            string analyzerId,
            string referenceDocsFolder,
            string referenceDocSasUrl,
            string referenceDocPath,
            string analyzer_template,
            string input_docs,
            bool skipAnalyze)
        {
            // Step 1: Generate reference data and upload to blob storage
            await service.GenerateKnowledgeBaseOnBlobAsync(referenceDocsFolder, referenceDocSasUrl, referenceDocPath, skipAnalyze: skipAnalyze);

            // Step 2: Validate that all reference data files are uploaded
            var files = Directory.GetFiles(referenceDocsFolder, "*.*", SearchOption.AllDirectories).ToList().ToHashSet();
            // check if the reference data is uploaded to the blob storage
            var blobClient = new BlobContainerClient(new Uri(referenceDocSasUrl));
            var blobFiles = new HashSet<string>();
            await foreach (BlobItem blobItem in blobClient.GetBlobsAsync(prefix: referenceDocPath))
            {
                var name = blobItem.Name.Substring(referenceDocPath.Length);
                if (!string.IsNullOrEmpty(name) && !name.EndsWith("/"))
                {
                    blobFiles.Add(name);
                }
            }

            var fileNames = files.Select(f => Path.GetRelativePath(referenceDocsFolder, f)).ToHashSet();

            if (skipAnalyze)
            {
                // For bonus scenarios, just check file presence
                foreach (var fileName in fileNames)
                {
                    // Check if the file exists in the blob storage
                    Assert.Contains(fileName, blobFiles);
                }
            }
            else
            {
                // For main scenarios, check for label and result files as well
                foreach (var fileName in fileNames)
                {
                    Assert.Contains(fileName, blobFiles);
                    Assert.Contains(fileName + client.GetOcrResultFileSuffix(), blobFiles);
                }
            }

            // Ensure knowledge source list file exists in blob
            Assert.Contains(client.GetKnowledgeSourceListFileName(), blobFiles);

            // Step 3: Create analyzer and verify schema
            JsonDocument resultJson = await service.CreateAnalyzerWithDefinedSchemaForProModeAsync(
                analyzerId: analyzerId,
                analyzerSchema: analyzer_template,
                proModeReferenceDocsStorageContainerSasUrl: referenceDocSasUrl,
                proModeReferenceDocsStorageContainerPathPrefix: referenceDocPath);

            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var warnings));
            Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
            Assert.True(result.TryGetProperty("fieldSchema", out JsonElement fieldSchema));
            Assert.True(fieldSchema.TryGetProperty("fields", out JsonElement fields));
            Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));

            if (skipAnalyze)
            {
                Assert.True(result.TryGetProperty("status", out var status));
                Assert.Equal("ready", status.ToString());
                Assert.True(fields.TryGetProperty("LineItemCorroboration", out var lineItemCorroboration));
                Assert.True(fieldSchema.TryGetProperty("definitions", out JsonElement definitions));
                Assert.True(definitions.TryGetProperty("LineItemAnalysisEntry", out var lineItemAnalysisEntry));
                Assert.True(!string.IsNullOrWhiteSpace(lineItemAnalysisEntry.ToString()));
            }
            else
            {
                Assert.True(fields.TryGetProperty("PaymentTermsInconsistencies", out JsonElement paymentTermsInconsistencies));
                Assert.True(fields.TryGetProperty("ItemInconsistencies", out JsonElement ItemInconsistencies));
                Assert.True(fields.TryGetProperty("BillingLogisticsInconsistencies", out JsonElement BillingLogisticsInconsistencies));
                Assert.True(fields.TryGetProperty("PaymentScheduleInconsistencies", out JsonElement PaymentScheduleInconsistencies));
                Assert.True(fields.TryGetProperty("TaxOrDiscountInconsistencies", out JsonElement TaxOrDiscountInconsistencies));
            }
        }

        /// <summary>
        /// Analyzes a document with a previously created analyzer in Pro Mode,
        /// verifying that output fields and markdown/tables are present and valid.
        /// </summary>
        /// <param name="analyzerId">The analyzer identifier.</param>
        /// <param name="input_docs">The location of input documents to analyze.</param>
        /// <param name="skipAnalyze">If true, runs validation for bonus scenarios.</param>
        private async Task AnalyzeDocumentWithDefinedSchemaForProModeAsync(string analyzerId, string input_docs, bool skipAnalyze)
        {
            JsonDocument resultJson = await service.AnalyzeDocumentWithDefinedSchemaForProModeAsync(
               analyzerId: analyzerId,
               fileLocation: input_docs);

            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var warnings));
            Assert.False(warnings.EnumerateArray().Any(), "The warnings array should be empty");
            Assert.True(result.TryGetProperty("contents", out JsonElement contents));
            Assert.True(contents[0].TryGetProperty("fields", out JsonElement fields));
            if(skipAnalyze)
            {
                Assert.True(fields.TryGetProperty("LineItemCorroboration", out var lineItemCorroboration));
                Assert.True(lineItemCorroboration.TryGetProperty("valueArray", out var valueArray));
                Assert.True(valueArray.EnumerateArray().Any());
            }
            else
            {
                Assert.True(fields.TryGetProperty("PaymentTermsInconsistencies", out JsonElement paymentTermsInconsistencies));
                Assert.True(fields.TryGetProperty("ItemInconsistencies", out JsonElement ItemInconsistencies));
                Assert.True(fields.TryGetProperty("BillingLogisticsInconsistencies", out JsonElement BillingLogisticsInconsistencies));
                Assert.True(fields.TryGetProperty("PaymentScheduleInconsistencies", out JsonElement PaymentScheduleInconsistencies));
                Assert.True(fields.TryGetProperty("TaxOrDiscountInconsistencies", out JsonElement TaxOrDiscountInconsistencies));
            }

            // Validate markdown, paragraphs, sections, tables in second content block
            Assert.True(contents[1].TryGetProperty("markdown", out JsonElement markdown));
            Assert.True(!string.IsNullOrWhiteSpace(markdown.ToString()));
            Assert.True(contents[1].TryGetProperty("paragraphs", out JsonElement paragraphs));
            Assert.True(paragraphs.EnumerateArray().Any(), "The paragraphs array is empty, expected at least one table.");
            Assert.True(contents[1].TryGetProperty("sections", out JsonElement sections));
            Assert.True(sections.EnumerateArray().Any(), "The sections array is empty, expected at least one table.");
            Assert.True(contents[1].TryGetProperty("tables", out JsonElement tables));
            Assert.True(tables.EnumerateArray().Any(), "The tables array is empty, expected at least one table.");
        }
    }
}
