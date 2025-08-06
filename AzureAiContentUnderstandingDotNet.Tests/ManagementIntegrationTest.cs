using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AzureAiContentUnderstandingDotNet.Tests
{
    public class ManagementIntegrationTest
    {
        private readonly IManagementService service;

        public ManagementIntegrationTest()
        {
            var host = Host.CreateDefaultBuilder()
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
                        opts.UserAgent = "azure-ai-content-understanding-dotnet/management";
                    });
                    services.AddTokenProvider();
                    services.AddHttpClient<AzureContentUnderstandingClient>();
                    services.AddSingleton<IManagementService, ManagementService>();
                })
                .Build();

            service = host.Services.GetService<IManagementService>()!;
        }

        [Fact]
        public async Task RunAsync()
        {
            Exception? serviceException = null;

            try
            {
                var id = $"analyzer-management-sample-{Guid.NewGuid()}";
                var analyzerTemplatePath = "./analyzer_templates/call_recording_analytics.json";

                // 1. Create a simple analyzer
                var analyzerId = await service!.CreateAnalyzerAsync(id, analyzerTemplatePath);
                Assert.NotNull(analyzerId);
                // 2. Get analyzer details
                Dictionary<string, object> details = await service.GetAnalyzerDetailsAsync(analyzerId);
                Assert.True(details.Any());
                Assert.True(details.ContainsKey("warnings"));
                Assert.True(details.TryGetValue("warnings", out var values));
                Assert.False(((JsonElement)values).EnumerateArray().Any());
                Assert.True(details.ContainsKey("mode"));
                Assert.Equal("standard", details["mode"].ToString());
                Assert.True(details.ContainsKey("status"));
                Assert.Equal("ready", details["status"].ToString());
                Assert.True(details.ContainsKey("fieldSchema"));
                Assert.True(((JsonElement)details["fieldSchema"]).TryGetProperty("fields", out var fields));
                Assert.True(!string.IsNullOrWhiteSpace(fields.GetRawText()));

                // 3. List all analyzers
                await service.ListAnalyzersAsync();

                // 4. Delete analyzer
                await service.DeleteAnalyzerAsync(analyzerId);
            }
            catch (Exception ex)
            {
                serviceException = ex;
            }

            // no exception should be thrown
            Assert.Null(serviceException);
        }
    }
}
