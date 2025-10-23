using Azure.AI.ContentUnderstanding;
using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Json;
using System.Text.Json;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Integration test for classifier and enhanced classifier workflows using the IClassifierService.
    /// This test covers classifier creation, document classification, and enhanced classifier processing.
    /// </summary>
    public class ClassifierIntegrationTest
    {
        private readonly IClassifierService service;

        /// <summary>
        /// Sets up dependency injection, configures the test host, and validates required configurations for classifier testing.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if required configuration values for "AZURE_CU_CONFIG:Endpoint" or "AZURE_CU_CONFIG:ApiVersion" are missing.
        /// </exception>
        public ClassifierIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
                    services.AddSingleton<IClassifierService, ClassifierService>();
                })
                .Build();

            service = host.Services.GetService<IClassifierService>()!;
        }

        /// <summary>
        /// Executes an integration test for classifier workflows:
        /// 1. Creates a basic classifier using a schema.
        /// 2. Classifies a document using the created classifier.
        /// 3. Processes a document using an enhanced classifier.
        /// Captures any exceptions and asserts that no unexpected errors occur.
        /// </summary>
        [Fact(DisplayName = "Classifier Integration Test")]
        [Trait("Category", "Integration")]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
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
                await ClassifyDocumentAsync(classifierId, classifierSchema, analyzerTemplatePath);

                // Classify a document using the enhanced classifier
                await ClassifyDocumentAsync(classifierId, enhancedClassifierSchema, analyzerTemplatePath);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }
            // Final assertion: No exception should be thrown during the workflow
            Assert.Null(serviceException);
        }

        /// <summary>
        /// Classifies a document using the specified classifier and validates the result.
        /// Asserts that classification results are returned for the document.
        /// </summary>
        /// <param name="classifierId">ID of the classifier.</param>
        /// <param name="fileLocation">Path to the document to be classified.</param>
        private async Task ClassifyDocumentAsync(string classifierId, ContentClassifier classifier, string fileLocation)
        {
            // Classify a document using the created classifier
            ClassifyResult? result = await service.ClassifyDocumentAsync(classifierId, classifier, fileLocation);
            Assert.NotNull(result);
            Assert.False(result.Warnings.Any(), "The warnings array should be empty");
            Assert.True(result.Contents.Any());

            var content = result.Contents[0];
            Assert.True(string.IsNullOrWhiteSpace(content.Markdown));
            Assert.True(content.Fields.Any());
        }
    }
}
