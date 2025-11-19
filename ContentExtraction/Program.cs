using ContentExtraction.Interfaces;
using ContentExtraction.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace ContentExtraction
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Create host and configure services (without deployment configuration)
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IContentExtractionService, ContentExtractionService>();
                }
            );

            // Verify client is available
            var client = host.Services.GetService<AzureContentUnderstandingClient>();
            if (client == null)
            {
                Console.WriteLine("❌ Failed to resolve AzureContentUnderstandingClient from DI container.");
                Console.WriteLine("   Please ensure AddContentUnderstandingClient() is called in ConfigureServices.");
                return;
            }

            // Print message about ModelDeploymentSetup
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Azure AI Content Understanding - Content Extraction Sample");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("⚠️  IMPORTANT: Before using prebuilt analyzers, you must configure model deployments.");
            Console.WriteLine();
            Console.WriteLine("   If you haven't already, please run the ModelDeploymentSetup sample first:");
            Console.WriteLine("   1. cd ../ModelDeploymentSetup");
            Console.WriteLine("   2. dotnet run");
            Console.WriteLine();
            Console.WriteLine("   This is a one-time setup that maps your deployed models to prebuilt analyzers.");
            Console.WriteLine("   See the main README.md for more details.");
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            var service = host.Services.GetRequiredService<IContentExtractionService>();

            while (true)
            {
                Console.WriteLine("Please enter a number to run sample: ");
                Console.WriteLine("[1] - Extract Document Content");
                Console.WriteLine("[2] - Extract Document Content from URL");
                Console.WriteLine("[3] - Extract Audio Content");
                Console.WriteLine("[4] - Extract Video Content");
                
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        var docFilePath = "invoice.pdf";
                        await service.AnalyzeDocumentAsync(docFilePath);
                        break;
                    case "2":
                        var documentUrl = "https://github.com/Azure-Samples/azure-ai-content-understanding-python/raw/refs/heads/main/data/invoice.pdf";
                        await service.AnalyzeDocumentFromUrlAsync(documentUrl);
                        break;
                    case "3":
                        var audioFilePath = "audio.wav";
                        await service.AnalyzeAudioAsync(audioFilePath);
                        break;
                    case "4":
                        var videoFilePath = "FlightSimulator.mp4";
                        await service.AnalyzeVideoAsync(videoFilePath);
                        break;
                    default:
                        Console.WriteLine("Invalid number, please retry to input");
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}