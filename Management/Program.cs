using ContentUnderstanding.Common;
using Management.Extensions;
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
