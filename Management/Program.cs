using ContentUnderstanding.Common.Extensions;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Text.Json;

namespace Management
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var services = await ContentUnderstandingBootstrapper.BootstrapAsync(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IManagementService, ManagementService>();
                }
            );

            if (services == null)
            {
                Console.WriteLine("Failed to initialize. Exiting...");
                return;
            }

            var service = services.GetRequiredService<IManagementService>();

            // Generate analyzer ID with timestamp
            string analyzerId = $"notebooks_sample_management_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // Create a custom analyzer using dictionary format
            Console.WriteLine($"🔧 Creating custom analyzer '{analyzerId}'...");

            var contentAnalyzer = new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-callCenter",
                ["description"] = "Sample call recording analytics",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["locales"] = new[] { "en-US" }
                },
                ["fieldSchema"] = new Dictionary<string, object>
                {
                    ["fields"] = new Dictionary<string, object>
                    {
                        ["Summary"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "generate",
                            ["description"] = "A one-paragraph summary"
                        },
                        ["Topics"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["description"] = "Top 5 topics mentioned",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string"
                            }
                        },
                        ["Companies"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["description"] = "List of companies mentioned",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string"
                            }
                        },
                        ["People"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "generate",
                            ["description"] = "List of people mentioned",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "object",
                                ["properties"] = new Dictionary<string, object>
                                {
                                    ["Name"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "string",
                                        ["description"] = "Person's name"
                                    },
                                    ["Role"] = new Dictionary<string, object>
                                    {
                                        ["type"] = "string",
                                        ["description"] = "Person's title/role"
                                    }
                                }
                            }
                        },
                        ["Sentiment"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["method"] = "classify",
                            ["description"] = "Overall sentiment",
                            ["enum"] = new[] { "Positive", "Neutral", "Negative" }
                        },
                        ["Categories"] = new Dictionary<string, object>
                        {
                            ["type"] = "array",
                            ["method"] = "classify",
                            ["description"] = "List of relevant categories",
                            ["items"] = new Dictionary<string, object>
                            {
                                ["type"] = "string",
                                ["enum"] = new[]
                                {
                                    "Agriculture",
                                    "Business",
                                    "Finance",
                                    "Health",
                                    "Insurance",
                                    "Mining",
                                    "Pharmaceutical",
                                    "Retail",
                                    "Technology",
                                    "Transportation"
                                }
                            }
                        }
                    }
                },
                ["models"] = new Dictionary<string, object>
                {
                    ["completion"] = "gpt-4.1"
                }
            };

            // Convert to JSON string
            string analyzerTemplatePath = JsonSerializer.Serialize(contentAnalyzer, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // 1. Create a simple analyzer
            await service!.CreateAnalyzerAsync(analyzerId, analyzerTemplatePath);

            // 2. List all analyzers
            await service.ListAnalyzersAsync();

            // 3. Get analyzer details
            await service.GetAnalyzerDetailsAsync(analyzerId);

            // 4. Delete analyzer
            await service.DeleteAnalyzerAsync(analyzerId);
        }
    }
}
