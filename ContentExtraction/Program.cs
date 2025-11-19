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

            var services = await ContentUnderstandingBootstrapper.BootstrapAsync(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IContentExtractionService, ContentExtractionService>();
                }
            );

            if (services == null)
            {
                Console.WriteLine("Failed to initialize. Exiting...");
                return;
            }

            var service = services.GetRequiredService<IContentExtractionService>();

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
                        var docFilePath = "./data/invoice.pdf";
                        await service.AnalyzeDocumentAsync(docFilePath);
                        break;
                    case "2":
                        var documentUrl = "https://github.com/Azure-Samples/azure-ai-content-understanding-python/raw/refs/heads/main/data/invoice.pdf";
                        await service.AnalyzeDocumentFromUrlAsync(documentUrl);
                        break;
                    case "3":
                        var audioFilePath = "./data/audio.wav";
                        await service.AnalyzeAudioAsync(audioFilePath);
                        break;
                    case "4":
                        var videoFilePath = "./data/FlightSimulator.mp4";
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