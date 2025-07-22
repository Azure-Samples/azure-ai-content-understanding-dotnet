using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Classifier {
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/classifier";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
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
            var (analyzerSchemaPath, enhancedSchemaPath) = ("./analyzer_templates/analyzer_schema.json", "./data/classifier/enhanced_schema.json");
            
            var classifierId = $"classifier-sample-{Guid.NewGuid()}";
            var classifierSchemaPath = "./data/classifier/schema.json";

            // Create a basic classifier
            await service.CreateClassifierAsync(classifierId, classifierSchemaPath);

            // Classify a document using the created classifier
            await service.ClassifyDocumentAsync(classifierId, analyzerTemplatePath);

            var analyzerId = $"analyzer-loan-application-{Guid.NewGuid()}";
            var enhancedClassifierId = await service.CreateEnhancedClassifierWithCustomAnalyzerAsync(analyzerId, analyzerSchemaPath, enhancedSchemaPath);

            // Process a document using the enhanced classifier
            await service.ProcessDocumentWithEnhancedClassifierAsync(enhancedClassifierId, analyzerTemplatePath);

            Console.WriteLine("## Summary and Next Steps");
            Console.WriteLine("Congratulations! You've successfully:");
            Console.WriteLine("1. Created a basic classifier to categorize documents.");
            Console.WriteLine("2. Created a custom analyzer to extract specific fields.");
            Console.WriteLine("3. Combined them into an enhanced classifier for intelligent document processing.\n");
        }
    }
}
