using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using FieldExtraction.Interfaces;
using FieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FieldExtraction
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/field_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IFieldExtractionService, FieldExtractionService>();

                })
                .Build();

            var service = host.Services.GetService<IFieldExtractionService>()!;

            var ExtractionTemplates = new Dictionary<string, (string, string)>
            {
                { "invoice", ("./analyzer_templates/invoice.json", "./data/invoice.pdf") },
                { "call_recording", ("./analyzer_templates/call_recording_analytics.json", "./data/callCenterRecording.mp3") },
                { "conversation_audio", ("./analyzer_templates/conversational_audio_analytics.json", "./data/callCenterRecording.mp3") },
                { "marketing_video", ("./analyzer_templates/marketing_video.json", "./data/FlightSimulator.mp4") }
            };

            string field_extraction_analyzerId = $"field-extraction-sample-{Guid.NewGuid()}";

            foreach (var item in ExtractionTemplates)
            {
                var (analyzerTemplatePath, analyzerSampleFilePath) = ExtractionTemplates[item.Key];

                await service.CreateAndUseAnalyzer(field_extraction_analyzerId, analyzerTemplatePath, analyzerSampleFilePath);
            }

        }
    }
}
