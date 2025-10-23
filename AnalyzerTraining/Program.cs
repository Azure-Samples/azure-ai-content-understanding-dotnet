using AnalyzerTraining.Interfaces;
using AnalyzerTraining.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace AnalyzerTraining
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
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
            Console.WriteLine("\n## Prepare training data");
            Console.WriteLine("\nIn this step, we will");
            Console.WriteLine("1. Check whether document files in local folder have corresponding `.labels.json` and `.result.json` files.");
            Console.WriteLine("2. Upload these files to the designated Azure blob storage.");
            Console.WriteLine("Please ensure you have the following information ready:");

            Console.WriteLine("TrainingDataSasUrl: Please paste the SAS URL that you have created in the last step and hit the [Enter] key.");

            string trainingDataSasUrl = Console.ReadLine() ?? string.Empty;

            Console.WriteLine("TrainingDataPath: Please write the folder name that you have created in the last step, such as labeling-data");
            
            string trainingDataPath = Console.ReadLine() ?? string.Empty;

            Console.WriteLine($"\nTrainingDataSasUrl: {trainingDataSasUrl}");
            Console.WriteLine($"TrainingDataPath: {trainingDataPath}\n");

            Console.WriteLine("Type yes and hit [Enter] to continue.");

            string? input = Console.ReadLine();
            
            if(input?.ToLower() != "yes")
            {
                Console.WriteLine("Exiting the sample.");
                return;
            }

            var service = host.Services.GetService<IAnalyzerTrainingService>()!;

            Console.WriteLine("\nStarting the field extraction process...");
            Console.WriteLine("Prepare training data and upload the prepared files to the designated Azure blob storage. Please wait...");

            var trainingDocsFolder = "./data/document_training";
            await service.GenerateTrainingDataOnBlobAsync(trainingDocsFolder, trainingDataSasUrl, trainingDataPath);

            var analyzerTemplatePath = "./analyzer_templates/receipt.json";
            var contentAnalyzer = await service.CreateAnalyzerAsync(analyzerTemplatePath, trainingDataSasUrl, trainingDataPath);

            var customAnalyzerSampleFilePath = "./data/receipt.png";
            await service.AnalyzeDocumentWithCustomAnalyzerAsync(contentAnalyzer.AnalyzerId, customAnalyzerSampleFilePath);

            // delete analyzer
            await service.DeleteAnalyzerAsync(contentAnalyzer.AnalyzerId);
        }
    }
}
