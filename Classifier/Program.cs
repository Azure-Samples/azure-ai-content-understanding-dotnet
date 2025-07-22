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

            var service = host.Services.GetService<IClassifierService>()!;
            var classifierId = $"classifier-sample-{Guid.NewGuid()}";
            var classifierSchemaPath = "./data/classifier/schema.json";

            await service.CreateClassifierAsync(classifierId, classifierSchemaPath);

            var analyzerTemplatePath = "./data/mixed_financial_docs.pdf";
            await service.ClassifyDocumentAsync(classifierId, analyzerTemplatePath);

            var analyzerId = $"analyzer-loan-application-{Guid.NewGuid()}";
            var (analyzerSchemaPath, enhancedSchemaPath) = ("./analyzer_templates/analyzer_schema.json", "./data/classifier/enhanced_schema.json");
            var enhancedClassifierId = await service.CreateEnhancedClassifierWithCustomAnalyzerAsync(analyzerId, analyzerSchemaPath, enhancedSchemaPath);
            
            await service.ProcessDocumentWithEnhancedClassifierAsync(enhancedClassifierId, analyzerTemplatePath);
        }
    }
}
