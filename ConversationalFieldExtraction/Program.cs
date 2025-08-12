using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using ConversationalFieldExtraction.Interfaces;
using ConversationalFieldExtraction.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConversationalFieldExtraction
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/conversational_field_extraction";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IConversationalFieldExtractionService, ConversationalFieldExtractionService>();
                })
                .Build();

            var service = host.Services.GetService<IConversationalFieldExtractionService>()!;

            var ExtractionTemplates = new Dictionary<string, (string, string)>
            {
                { "call_recording_pretranscribe_batch", ("./analyzer_templates/call_recording_analytics_text.json", "./data/batch_pretranscribed.json") },
                { "call_recording_pretranscribe_fast", ("./analyzer_templates/call_recording_analytics_text.json", "./data/fast_pretranscribed.json") },
                { "call_recording_pretranscribe_cu", ("./analyzer_templates/call_recording_analytics_text.json", "./data/cu_pretranscribed.json") }
            };

            var analyzerId = $"conversational-field-extraction-sample-{Guid.NewGuid()}";

            foreach (var item in ExtractionTemplates)
            {
                // Extract the template path and sample file path from the dictionary
                var (analyzerTemplatePath, analyzerSampleFilePath) = ExtractionTemplates[item.Key];

                // Create the analyzer from the template
                await service.CreateAnalyzerFromTemplateAsync(analyzerId, analyzerTemplatePath);

                // Extract fields using the created analyzer
                await service.ExtractFieldsWithAnalyzerAsync(analyzerId, analyzerSampleFilePath);

                // Clean up the analyzer after use
                await service.DeleteAnalyzerAsync(analyzerId);
            }
        }
    }
}
