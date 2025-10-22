using ContentExtraction.Interfaces;
using ContentExtraction.Services;
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

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddContentUnderstandingClient(context.Configuration);
                    services.AddSingleton<IContentExtractionService, ContentExtractionService>();
                })
                .Build();

            var service = host.Services.GetService<IContentExtractionService>()!;

            while(true)
            {
                Console.WriteLine("Please enter a number to run sample: ");
                Console.WriteLine("[1] - Extract Document Content");
                Console.WriteLine("[2] - Extract Audio Content");
                Console.WriteLine("[3] - Extract Video Content");
                Console.WriteLine("[4] - Extract Video Content With Face ");
                
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        var docFilePath = "./data/mixed_financial_docs.pdf";
                        await service.AnalyzeDocumentAsync(docFilePath);
                        break;
                    case "2":
                        var audioFilePath = "./data/audio.wav";
                        await service.AnalyzeAudioAsync(audioFilePath);
                        break;
                    case "3":
                        var videoFilePath = "./data/FlightSimulator.mp4";
                        await service.AnalyzeVideoAsync(videoFilePath);
                        break;
                    case "4":
                        var videoWithFaceFilePath = "./data/FlightSimulator.mp4";
                        await service.AnalyzeVideoWithFaceAsync(videoWithFaceFilePath);
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