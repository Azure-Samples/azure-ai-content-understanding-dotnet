using ContentExtraction.Extensions;
using ContentExtraction.Interfaces;
using ContentExtraction.Services;
using ContentUnderstanding.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContentExtraction
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/content_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
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
                        var docFilePath = "./data/invoice.pdf";
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