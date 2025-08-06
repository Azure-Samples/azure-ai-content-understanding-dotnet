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

namespace AzureAiContentUnderstandingDotNet.Tests
{
    public class FieldExtractionProModeIntegrationTest
    {
        private readonly IFieldExtractionProModeService service;
        private readonly AzureContentUnderstandingClient client;
        private const string referenceDocSasUrl = "https://<your_storage_account_name>.blob.core.windows.net/<your_container_name>?<your_sas_token>";

        public FieldExtractionProModeIntegrationTest()
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
        /// Executes an integration test for the Field Extraction Pro Mode feature.
        /// </summary>
        /// <remarks>This method validates the presence of required input files and analyzer templates, 
        /// then performs document analysis using a defined schema for Pro Mode.  If any exception occurs during the
        /// test execution, it is captured and asserted to ensure no errors.</remarks>
        /// <returns>A task that represents the asynchronous operation of the integration test.</returns>
        [Fact(DisplayName = "Field Extraction Pro Mode Integration Test")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                var referenceDocPath = $"test_field_extraction_pro_mode_data_dotnet_{DateTime.Now.ToString("yyyyMMddHHmmss")}/";
                var referenceDocsFolder = "./data/field_extraction_pro_mode/invoice_contract_verification/reference_docs";
                var analyzer_template = "./analyzer_templates/invoice_contract_verification_pro_mode.json";
                var input_docs = "./data/field_extraction_pro_mode/invoice_contract_verification/input_docs";

                Assert.True(Directory.GetFiles(referenceDocsFolder).Length > 0);
                Assert.True(File.Exists(analyzer_template));
                Assert.True(Directory.GetFiles(input_docs).Length > 0);

                var analyzerId = $"pro-mode-sample-{Guid.NewGuid()}";

                // Create analyzer with defined schema
                await CreateAnalyzerWithDefinedSchemaForProModeAsync(
                    analyzerId,
                    referenceDocsFolder: referenceDocsFolder,
                    referenceDocSasUrl: referenceDocSasUrl,
                    referenceDocPath: referenceDocPath,
                    analyzer_template: analyzer_template,
                    input_docs: input_docs,
                    skipAnalyze: false);

                // Analyze document with defined schema
                await AnalyzeDocumentWithDefinedSchemaForProModeAsync(
                    analyzerId: analyzerId,
                    input_docs: input_docs,
                    skipAnalyze: false);

                // Delete the analyzer after testing
                await service.DeleteAnalyzerAsync(analyzerId);

                // Bonus
                var analyzer_template_for_bonus_sample = "./analyzer_templates/insurance_claims_review_pro_mode.json";
                var input_docs_for_bonus_sample = "./data/field_extraction_pro_mode/insurance_claims_review/input_docs";
                var reference_docs_for_bonus_sample = "./data/field_extraction_pro_mode/insurance_claims_review/reference_docs";
                var analyzer_id_for_bonus_sample = $"pro-mode-sample-bonus-{Guid.NewGuid()}";

                // Validate the bonus sample files
                await CreateAnalyzerWithDefinedSchemaForProModeAsync(
                    analyzerId: analyzer_id_for_bonus_sample,
                    referenceDocsFolder: reference_docs_for_bonus_sample,
                    referenceDocSasUrl: referenceDocSasUrl,
                    referenceDocPath: referenceDocPath,
                    analyzer_template: analyzer_template_for_bonus_sample,
                    input_docs: input_docs_for_bonus_sample,
                    skipAnalyze: true);

                // Analyze the bonus sample document
                await AnalyzeDocumentWithDefinedSchemaForProModeAsync(
                    analyzerId: analyzer_id_for_bonus_sample,
                    input_docs: input_docs_for_bonus_sample,
                    skipAnalyze: true);

                // Dlete the bonus sample analyzer after testing
                await service.DeleteAnalyzerAsync(analyzer_id_for_bonus_sample);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Analyzes a document using a defined schema in "Pro Mode" by leveraging reference documents stored in blob
        /// storage.
        /// </summary>
        /// <remarks>This method performs the following operations: 1. Generates a knowledge base from the
        /// reference documents and uploads it to blob storage. 2. Validates that the reference data files are correctly
        /// uploaded to the specified blob storage location. 3. Creates an analyzer with the provided schema and
        /// reference documents. 4. Executes the analysis on the input document using the created analyzer.  Ensure that
        /// the reference documents folder contains all necessary files and that the blob storage SAS URL provides
        /// appropriate access permissions for reading and writing.</remarks>
        /// <param name="referenceDocsFolder">The local folder path containing the reference documents used to generate the knowledge base.</param>
        /// <param name="referenceDocSasUrl">The SAS URL of the blob storage container where the reference documents will be uploaded.</param>
        /// <param name="referenceDocPath">The path prefix within the blob storage container where the reference documents will be stored.</param>
        /// <param name="analyzer_template">The JSON schema template defining the structure and rules for the analyzer.</param>
        /// <param name="input_docs">The location of the input document to be analyzed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task CreateAnalyzerWithDefinedSchemaForProModeAsync(
            string analyzerId,
            string referenceDocsFolder,
            string referenceDocSasUrl,
            string referenceDocPath,
            string analyzer_template,
            string input_docs,
            bool skipAnalyze)
        {
            // Generate reference data and upload it to blob storage
            await service.GenerateKnowledgeBaseOnBlobAsync(referenceDocsFolder, referenceDocSasUrl, referenceDocPath, skipAnalyze: skipAnalyze);

            // Validate that the reference data files are correctly uploaded
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
                foreach(var fileName in fileNames)
                {
                    // Check if the file exists in the blob storage
                    Assert.Contains(fileName, blobFiles);
                }
            }
            else
            {
                // Check if all files in the referenceDocsFolder have corresponding label and result files
                foreach (var fileName in fileNames)
                {
                    Assert.Contains(fileName, blobFiles);
                    Assert.Contains(fileName + client.GetOcrResultFileSuffix(), blobFiles);
                }
            }

            Assert.Contains(client.GetKnowledgeSourceListFileName(), blobFiles);

            JsonDocument resultJson = await service.CreateAnalyzerWithDefinedSchemaForProModeAsync(
                analyzerId: analyzerId,
                analyzerSchema: analyzer_template,
                proModeReferenceDocsStorageContainerSasUrl: referenceDocSasUrl,
                proModeReferenceDocsStorageContainerPathPrefix: referenceDocPath);

            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var values));
            Assert.False(values.EnumerateArray().Any(), "The warnings array should be empty");
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

        private async Task AnalyzeDocumentWithDefinedSchemaForProModeAsync(string analyzerId, string input_docs, bool skipAnalyze)
        {
            JsonDocument resultJson = await service.AnalyzeDocumentWithDefinedSchemaForProModeAsync(
               analyzerId: analyzerId,
               fileLocation: input_docs);

            Assert.NotNull(resultJson);
            Assert.True(resultJson.RootElement.TryGetProperty("result", out JsonElement result));
            Assert.True(result.TryGetProperty("warnings", out var values));
            Assert.False(values.EnumerateArray().Any(), "The warnings array should be empty");
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
