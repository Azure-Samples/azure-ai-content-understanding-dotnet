using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtractionProMode.Interfaces;
using FieldExtractionProMode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;

namespace FieldExtractionProMode
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
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

            var service = host.Services.GetService<IFieldExtractionProModeService>()!;

            Console.WriteLine("# Conduct complex analysis with Pro mode");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine("> ");
            Console.WriteLine("> Note: Pro mode is currently available only for `document` data.");
            Console.WriteLine("> [Supported file types](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/service-limits#document-and-text): pdf, tiff, jpg, jpeg, png, bmp, heif");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine("\n");
            Console.WriteLine("This sample demonstrates how to use [Pro mode] in Azure AI Content Understanding to enhance your analyzer with multiple inputs and optional reference data.");
            Console.WriteLine("Pro mode is designed for advanced use cases, particularly those requiring multi-step reasoning, and complex decision-making (for instance, identifying inconsistencies, drawing inferences, and making sophisticated decisions).");
            Console.WriteLine("Pro mode allows input from multiple content files and includes the option to provide reference data at analyzer creation time.");
            Console.WriteLine("\n");
            Console.WriteLine("In this walkthrough, you'll learn how to:");
            Console.WriteLine("1. Create an analyzer with a schema and reference data.");
            Console.WriteLine("2. Analyze your files using Pro mode.");
            Console.WriteLine("\n");
            Console.WriteLine("For more details on Pro mode, see the [Azure AI Content Understanding: Standard and Pro Modes](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/standard-pro-modes) documentation.");
            Console.WriteLine("## Prerequisites\n");
            Console.WriteLine("1. Ensure Azure AI service is configured following [steps](../README.md#configure-azure-ai-service-resource)");
            Console.WriteLine("2. If using reference documents, please follow [Set env for reference doc](../docs/set_env_for_training_data_and_reference_doc.md) to set up `ReferenceDocSasUrl` and `ReferenceDocPath`.");

            Console.WriteLine("ReferenceDocSasUrl: Please paste the SAS URL that you have created in the last step and hit the [Enter] key.");
            string referenceDocSasUrl = Console.ReadLine() ?? string.Empty;
            Console.WriteLine("ReferenceDocPath: Please paste the folder path within the container for uploading reference docs.");
            string referenceDocPath = Console.ReadLine() ?? string.Empty;

            // Analyzer template and local files setup
            // - analyzer_template: In this sample we define an analyzer template for invoice-contract verification.
            // - input_docs: We can have multiple input document files in one folder or designate a single document file location.
            // - reference_docs: During analyzer creation, we can provide documents that can aid in providing context that the analyzer references at inference time.We will get OCR results for these files if needed, generate a reference JSONL file, and upload these files to a designated Azure blob storage.
            // Note: For example, if you're looking to analyze invoices to ensure they're consistent with a contractual agreement, you can supply the invoice and other relevant documents (for example, a purchase order) as inputs, and supply the contract files as reference data.
            // The service applies reasoning to validate the input documents according to your schema, which might be to identify discrepancies to flag for further review.
            var analyzer_template = "./analyzer_templates/invoice_contract_verification_pro_mode.json";
            var input_docs = "./data/field_extraction_pro_mode/invoice_contract_verification/input_docs";
            var reference_docs = "./data/field_extraction_pro_mode/invoice_contract_verification/reference_docs";

            var analyzer_template_json = await File.ReadAllTextAsync(analyzer_template);
            Console.WriteLine($"The analyzer template of Pro mode: {analyzer_template_json}");
            Console.WriteLine("Note: Reference documents are optional in Pro mode. You can run Pro mode using just input documents. \nFor example, the service can reason across two or more input files even without any reference data. \nPlease skip or comment out below section to skip the preparation of reference documents.");

            // Set skip_analyze to True if you already have OCR results for the documents in the reference_docs folder.
            // Please name the OCR result files with the same name as the original document files including its extension, and add the suffix ".result.json".
            // For example, if the original document is "invoice.pdf", the OCR result file should be named "invoice.pdf.result.json".
            // NOTE: Please comment out the follwing line if you don't have any reference documents.
            await service.GenerateKnowledgeBaseOnBlobAsync(
                referenceDocsFolder: reference_docs,
                storageContainerSasUrl: referenceDocSasUrl,
                storageContainerPathPrefix: referenceDocPath,
                skipAnalyze: false);

            Console.WriteLine("## Create analyzer with defined schema for Pro mode");
            Console.WriteLine("Before creating the analyzer, you should fill in the constant ANALYZER_ID with a relevant name to your task. Here, we generate a unique suffix so this cell can be run multiple times to create different analyzers.");
            // Note: The analyzer ID must be unique within your Azure AI Content Understanding service.
            var analyzerId = $"pro-mode-sample-{Guid.NewGuid()}";
            await service.CreateAnalyzerWithDefinedSchemaForProModeAsync(
                analyzerId: analyzerId,
                analyzerSchema: analyzer_template_json,
                proModeReferenceDocsStorageContainerSasUrl: referenceDocSasUrl,
                proModeReferenceDocsStorageContainerPathPrefix: referenceDocPath);

            Console.WriteLine($"Analyzer created with ID: {analyzerId}");
            Console.WriteLine("Use created analyzer to analyze the input documents.");
            Console.WriteLine("After the analyzer is successfully created, we can use it to analyze our input files.");
            Console.WriteLine($"NOTE: Pro mode does multi-step reasoning and may take a longer time to analyze.");

            // Analyze the input documents using the created analyzer
            await service.AnalyzeDocumentWithDefinedSchemaForProModeAsync(
                analyzerId: analyzerId,
                fileLocation: input_docs);

            Console.WriteLine("Analysis completed successfully.");

            // (optional) Delete the analyzer
            // This snippet is not required, but it's only used to prevent the testing analyzer from residing in your service. Without deletion, the analyzer will remain in your service for subsequent reuse.
            await service.DeleteAnalyzerAsync(analyzerId);

            Console.WriteLine("## Bonus sample.");
            Console.WriteLine("We would like to introduce another sample to highlight how Pro mode supports multi-document input and advanced reasoning. \nUnlike Document Standard Mode, which processes one document at a time, Pro mode can analyze multiple documents within a single analysis call. \nWith Pro mode, the service not only processes each document independently, but also cross-references the documents to perform reasoning across them, enabling deeper insights and validation.");
            Console.WriteLine("First, we need to set up variables for the second sample: ");

            var analyzer_template_for_bonus_sample = "./analyzer_templates/insurance_claims_review_pro_mode.json";
            var input_docs_for_bonus_sample = "./data/field_extraction_pro_mode/insurance_claims_review/input_docs";
            var reference_docs_for_bonus_sample = "./data/field_extraction_pro_mode/insurance_claims_review/reference_docs";        
            var analyzer_id_for_bonus_sample = $"pro-mode-sample-bonus-{Guid.NewGuid()}";

            Console.WriteLine($"Start generating knowledge base for the second sample...");
            await service.GenerateKnowledgeBaseOnBlobAsync(
                referenceDocsFolder: reference_docs_for_bonus_sample,
                storageContainerSasUrl: referenceDocSasUrl,
                storageContainerPathPrefix: referenceDocPath,
                skipAnalyze: false);

            Console.WriteLine($"Start creating analyzer with defined schema for Pro mode for the second sample...");
            var analyzer_template_json_for_bonus_sample = await File.ReadAllTextAsync(analyzer_template_for_bonus_sample);
            
            Console.WriteLine($"The analyzer template of Pro mode: {analyzer_template_json}");
            await service.CreateAnalyzerWithDefinedSchemaForProModeAsync(
                analyzerId: analyzer_id_for_bonus_sample,
                analyzerSchema: analyzer_template_json_for_bonus_sample,
                proModeReferenceDocsStorageContainerSasUrl: referenceDocSasUrl,
                proModeReferenceDocsStorageContainerPathPrefix: referenceDocPath);

            Console.WriteLine($"Analyze the multiple input documents with the second analyzer with ID: {analyzer_id_for_bonus_sample}");
            Console.WriteLine("Please note that the [input_docs](../data/field_extraction_pro_mode/insurance_claims_review/input_docs/) directory contains two PDF files as input: one is a car accident report, and the other is a repair estimate.");
            Console.WriteLine("The first document includes details such as the car’s license plate number, vehicle model, and other incident-related information.");
            Console.WriteLine("The second document provides a breakdown of the estimated repair costs.");
            Console.WriteLine("Due to the complexity of this multi-document scenario and the processing involved, it may take a few minutes to generate the results.");
            Console.WriteLine("Start analyzing input documents for the second sample...");

            await service.AnalyzeDocumentWithDefinedSchemaForProModeAsync(
                analyzerId: analyzer_id_for_bonus_sample,
                fileLocation: input_docs_for_bonus_sample);

            Console.WriteLine("Analysis for the second sample completed successfully.");

            // (optional) Delete the analyzer for the second sample
            await service.DeleteAnalyzerAsync(analyzer_id_for_bonus_sample);
        }
    }
}
