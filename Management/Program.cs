using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Management
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    string endpoint = context.Configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        throw new ArgumentException("Endpoint must be provided in appsettings.json.");
                    }

                    string apiVersion = context.Configuration.GetValue<string>("AZURE_APIVERSION") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(apiVersion))
                    {
                        throw new ArgumentException("API version must be provided in appsettings.json.");
                    }

                    services.AddConfigurations(opts =>
                    {
                        opts.Endpoint = endpoint;
                        opts.ApiVersion = apiVersion;
                        opts.SubscriptionKey = context.Configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_KEY") ?? string.Empty;

                        // This header is used for sample usage telemetry, please comment out this line if you want to opt out.
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/management";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IManagementService, ManagementService>();
                })
                .Build();

            var service = host.Services.GetService<IManagementService>()!;

            var analyzerId = $"analyzer-management-sample-{Guid.NewGuid()}";
            var analyzerTemplatePath = "./analyzer_templates/call_recording_analytics.json";
            // 1. Create a simple analyzer
            var management_analyzerId = await service!.CreateAnalyzerAsync(analyzerId, analyzerTemplatePath);

            // 2. Get analyzer details
            await service.GetAnalyzerDetailsAsync(management_analyzerId);

            // 3. List all analyzers
            await service.ListAnalyzersAsync();

            // 4. Delete analyzer
            await service.DeleteAnalyzerAsync(management_analyzerId);
        }
    }
}
