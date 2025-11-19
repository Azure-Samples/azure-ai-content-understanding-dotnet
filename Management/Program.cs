using ContentUnderstanding.Common.Extensions;
using Management.Interfaces;
using Management.Services;
using Microsoft.Extensions.Configuration;
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

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Azure AI Content Understanding - Management Sample");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            var service = services.GetRequiredService<IManagementService>();
            var configuration = services.GetRequiredService<IConfiguration>();

            // Get model deployment names from configuration
            string? GetConfigValue(string key) => configuration.GetValue<string>(key) ?? Environment.GetEnvironmentVariable(key);

            string? gpt41Deployment = GetConfigValue("GPT_4_1_DEPLOYMENT");
            string? gpt41MiniDeployment = GetConfigValue("GPT_4_1_MINI_DEPLOYMENT");
            string? textEmbedding3LargeDeployment = GetConfigValue("TEXT_EMBEDDING_3_LARGE_DEPLOYMENT");

            // 1. Update defaults (set model deployment mappings)
            Console.WriteLine("=== Update Defaults ===");
            if (!string.IsNullOrEmpty(gpt41Deployment) && !string.IsNullOrEmpty(gpt41MiniDeployment) && !string.IsNullOrEmpty(textEmbedding3LargeDeployment))
            {
                Console.WriteLine("Configuring default model deployments...");
                Console.WriteLine($"   GPT-4.1 deployment: {gpt41Deployment}");
                Console.WriteLine($"   GPT-4.1-mini deployment: {gpt41MiniDeployment}");
                Console.WriteLine($"   text-embedding-3-large deployment: {textEmbedding3LargeDeployment}");
                Console.WriteLine();

                var modelDeployments = new Dictionary<string, string?>
                {
                    ["gpt-4.1"] = gpt41Deployment,
                    ["gpt-4.1-mini"] = gpt41MiniDeployment,
                    ["text-embedding-3-large"] = textEmbedding3LargeDeployment
                };

                await service.UpdateDefaultsAsync(modelDeployments);
            }
            else
            {
                Console.WriteLine("⚠️  Warning: Model deployment configuration not found in appsettings.json or environment variables.");
                Console.WriteLine("   Skipping defaults update. Model deployments may not be configured.");
            }
            Console.WriteLine();

            // 2. Get defaults (retrieve model deployment mappings)
            Console.WriteLine("=== Get Defaults ===");
            await service.GetDefaultsAsync();
            Console.WriteLine();

            // Generate analyzer ID with timestamp
            string analyzerId = $"management_sample_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // Create a custom analyzer using dictionary format
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

            // 1. Create a simple analyzer
            Console.WriteLine("=== Create Analyzer ===");
            await service!.CreateAnalyzerAsync(analyzerId, contentAnalyzer);
            Console.WriteLine();

            // 2. List all analyzers
            Console.WriteLine("=== List All Analyzers ===");
            await service.ListAnalyzersAsync();

            // 3. Get analyzer details
            Console.WriteLine("=== Get Analyzer Details ===");
            await service.GetAnalyzerDetailsAsync(analyzerId);
            Console.WriteLine();

            // 4. Delete analyzer
            Console.WriteLine("=== Delete Analyzer ===");
            await service.DeleteAnalyzerAsync(analyzerId);
        }
    }
}
