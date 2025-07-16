using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

            Console.WriteLine("# Enhance your analyzer with labeled data");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine(">");
            Console.WriteLine("> Note: Currently this feature is only available for analyzer scenario is `document`");
            Console.WriteLine(">");
            Console.WriteLine("> #################################################################################");
            Console.WriteLine("In your own project, you will use [Azure AI Foundry](https://learn.microsoft.com/en-us/azure/ai-services/content-understanding/quickstart/use-ai-foundry) to use the labeling tool to annotate your data.\n");
            Console.WriteLine("In this sample we will demonstrate after you have the labeled data, how to create analyzer with them and analyze your files.\n");
            Console.WriteLine("## Prerequisites\n");
            Console.WriteLine("1. Ensure Azure AI service is configured following [steps](../README.md#configure-azure-ai-service-resource)\n");
            Console.WriteLine("2. Follow steps in [Set labeled data](../docs/set_env_for_labeled_data.md) to add training data related 'TrainingDataSasUrl' and 'TrainingDataPath' in ContentUnderstanding.Common/appsettings.json.\n");
            Console.WriteLine("3. Install packages needed to run the sample.\n");

            Console.WriteLine("Press [yes] to continue.");

            string? input = Console.ReadLine();
            
            if(input?.ToLower() != "yes")
            {
                Console.WriteLine("Exiting the sample.");
                return;
            }

            var service = host.Services.GetService<IAnalyzerTrainingService>()!;
            var options = host.Services.GetService<IOptions<ContentUnderstandingOptions>>()!;

            Console.WriteLine($"\nTrainingDataSasUrl: {options.Value.TrainingDataSasUrl}");
            Console.WriteLine($"TrainingDataPath: {options.Value.TrainingDataPath}\n");

            var analyzerTemplatePath = "./analyzer_templates/receipt.json";
            var analyzerId = await service.CreateAnalyzerAsync(analyzerTemplatePath, options.Value.TrainingDataSasUrl, options.Value.TrainingDataPath);

            var customAnalyzerSampleFilePath = "./data/receipt.png";
            await service.AnalyzeDocumentWithCustomAnalyzerAsync(analyzerId, customAnalyzerSampleFilePath);

            // delete analyzer
            await service.DeleteAnalyzerAsync(analyzerId);
        }
    }
}
