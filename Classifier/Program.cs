using Azure.AI.ContentUnderstanding;
using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace Classifier {
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
                    services.AddSingleton<IClassifierService, ClassifierService>();
                })
                .Build();

            Console.WriteLine("# Classifier and Analyzer sample");
            Console.WriteLine("This sample demonstrates how to use Azure AI Content Understanding service to:\n");
            Console.WriteLine("1. Create a classifier to categorize documents\n");
            Console.WriteLine("2. Create a custom analyzer to extract specific fields\n");
            Console.WriteLine("3. Combine classifier and analyzers to classify, optionally split, and analyze documents in a flexible processing pipeline\n");
            Console.WriteLine("If you'd like to learn more before getting started, see the official documentation:\r\n[Understanding Classifiers in Azure AI Services](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/concepts/classifier)");
            Console.WriteLine("## Prerequisites\n");
            Console.WriteLine("Ensure Azure AI service is configured following [steps](../README.md#configure-azure-ai-service-resource)\n");

            var service = host.Services.GetService<IClassifierService>()!;
            var analyzerTemplatePath = "./data/mixed_financial_docs.pdf";
            var classifierId = $"analyzer-loan-application-{Guid.NewGuid()}";
            var contentAnalyzer = new ContentAnalyzer
            {
                BaseAnalyzerId = "prebuilt-documentAnalyzer",
                Description = "Loan application analyzer - extracts key information from loan application",
                Config = new ContentAnalyzerConfig
                {
                    ReturnDetails = true,
                    EnableLayout = true,
                    EnableFormula = false,
                    EstimateFieldSourceAndConfidence = true,
                    DisableContentFiltering = false
                },
                FieldSchema = new ContentFieldSchema(fields: new Dictionary<string, ContentFieldDefinition>
                {
                    ["ApplicationDate"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.Date,
                        Method = GenerationMethod.Generate,
                        Description = "The date when the loan application was submitted."
                    },
                    ["ApplicantName"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.String,
                        Method = GenerationMethod.Generate,
                        Description = "The full name of the loan applicant or company."
                    },
                    ["LoanAmountRequested"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.Number,
                        Method = GenerationMethod.Generate,
                        Description = "The total amount of loan money requested by the applicant."
                    },
                    ["LoanPurpose"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.String,
                        Method = GenerationMethod.Generate,
                        Description = "The stated purpose or reason for the loan."
                    },
                    ["CreditScore"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.Number,
                        Method = GenerationMethod.Generate,
                        Description = "The credit score of the applicant, if available."
                    },
                    ["Summary"] = new ContentFieldDefinition
                    {
                        Type = ContentFieldType.String,
                        Method = GenerationMethod.Generate,
                        Description = "A brief overview of the loan application details."
                    }
                })
            };

            // Create content classifier with categories
            var classifierSchema = new ContentClassifier(categories: new Dictionary<string, ClassifierCategoryDefinition>
            {
                ["Loan application"] = new ClassifierCategoryDefinition
                {
                    Description = "Documents submitted by individuals or businesses to request funding, typically including personal or business details, financial history, loan amount, purpose, and supporting documentation."
                },
                ["Invoice"] = new ClassifierCategoryDefinition
                {
                    Description = "Billing documents issued by sellers or service providers to request payment for goods or services, detailing items, prices, taxes, totals, and payment terms."
                },
                ["Bank_Statement"] = new ClassifierCategoryDefinition
                {
                    Description = "Official statements issued by banks that summarize account activity over a period, including deposits, withdrawals, fees, and balances."
                },
            })
            {
                SplitMode = ClassifierSplitMode.Auto
            };

            // create a enhanced classifier schema that includes the custom analyzer
            var enhancedClassifierSchema = new ContentClassifier(categories: new Dictionary<string, ClassifierCategoryDefinition>
            {
                ["Loan application"] = new ClassifierCategoryDefinition
                {
                    AnalyzerId = classifierId,
                    Description = "Documents submitted by individuals or businesses to request funding, typically including personal or business details, financial history, loan amount, purpose, and supporting documentation."
                },
                ["Invoice"] = new ClassifierCategoryDefinition
                {
                    Description = "Billing documents issued by sellers or service providers to request payment for goods or services, detailing items, prices, taxes, totals, and payment terms."
                },
                ["Bank_Statement"] = new ClassifierCategoryDefinition
                {
                    Description = "Official statements issued by banks that summarize account activity over a period, including deposits, withdrawals, fees, and balances."
                },
            })
            {
                SplitMode = ClassifierSplitMode.Auto,
            };

            // Classify a document using the created classifier
            await service.ClassifyDocumentAsync(classifierId, classifierSchema, analyzerTemplatePath);

            // Classify a document using the enhanced classifier
            await service.ClassifyDocumentAsync(classifierId, enhancedClassifierSchema, analyzerTemplatePath);

            Console.WriteLine("## Summary");
            Console.WriteLine("Congratulations! You've successfully:");
            Console.WriteLine("1. Created a basic classifier to categorize documents.");
            Console.WriteLine("2. Created a custom analyzer to extract specific fields.");
            Console.WriteLine("3. Combined them into an enhanced classifier for intelligent document processing.\n");
        }
    }
}
