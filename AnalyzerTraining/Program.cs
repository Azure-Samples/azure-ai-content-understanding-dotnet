using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AnalyzerTraining
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/analyzer_training";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IAnalyzerTrainingService, AnalyzerTrainingService>();

                })
                .Build();

            IAnalyzerTrainingService service = host.Services.GetService<IAnalyzerTrainingService>()!;
            var analyzerTemplatePath = "./analyzer_templates/receipt.json";

            var analyzerId = await service.CreateAnalyzerAsync(analyzerTemplatePath);

            var customAnalyzerSampleFilePath = "./data/receipt.png";
            await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);

            // delete analyzer
            await service.DeleteAnalyzerAsync(analyzerId);
        }
    }
}
