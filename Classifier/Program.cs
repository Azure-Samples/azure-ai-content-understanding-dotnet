using Classifier.Interfaces;
using Classifier.Services;
using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Classifier
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Validate endpoint configuration BEFORE creating the host
            // This prevents the "Client created successfully" message from appearing with invalid endpoint
            // Use the same approach as ContentUnderstandingBootstrapper to find appsettings.json
            var contentRoot = Directory.GetCurrentDirectory();
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir) && File.Exists(Path.Combine(assemblyDir, "appsettings.json")))
                {
                    contentRoot = assemblyDir;
                }
            }

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(contentRoot)
                .AddJsonFile("appsettings.json", optional: true);
            var tempConfig = configBuilder.Build();
            
            var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")
                ?? tempConfig.GetValue<string>("AZURE_AI_ENDPOINT");

            if (string.IsNullOrEmpty(endpoint) || 
                endpoint.Contains("YOUR_AI_FOUNDRY_RESOURCE", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("❌ Error: AZURE_AI_ENDPOINT is not configured or still contains placeholder value.");
                Console.WriteLine();
                Console.WriteLine("Please configure your endpoint:");
                Console.WriteLine("1. Edit ContentUnderstanding.Common/appsettings.json and set AZURE_AI_ENDPOINT to your actual endpoint");
                Console.WriteLine("   Example: https://your-resource-name.services.ai.azure.com");
                Console.WriteLine();
                Console.WriteLine("2. Or set the environment variable:");
                Console.WriteLine("   export AZURE_AI_ENDPOINT=\"https://your-resource-name.services.ai.azure.com\"");
                Console.WriteLine();
                Console.WriteLine("See the main README.md for configuration instructions.");
                return;
            }

            // Create host and configure services (without deployment configuration)
            var host = ContentUnderstandingBootstrapper.CreateHost(
                configureServices: (context, services) =>
                {
                    services.AddSingleton<IClassifierService, ClassifierService>();
                }
            );

            // Verify client is available
            var client = host.Services.GetService<AzureContentUnderstandingClient>();
            if (client == null)
            {
                Console.WriteLine("❌ Failed to resolve AzureContentUnderstandingClient from DI container.");
                Console.WriteLine("   Please ensure AddContentUnderstandingClient() is called in ConfigureServices.");
                return;
            }

            // Print message about ModelDeploymentSetup
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Azure AI Content Understanding - Classifier Sample");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("⚠️  IMPORTANT: Before using prebuilt analyzers, you must configure model deployments.");
            Console.WriteLine();
            Console.WriteLine("   If you haven't already, please run the ModelDeploymentSetup sample first:");
            Console.WriteLine("   1. cd ../ModelDeploymentSetup");
            Console.WriteLine("   2. dotnet run");
            Console.WriteLine();
            Console.WriteLine("   This is a one-time setup that maps your deployed models to prebuilt analyzers.");
            Console.WriteLine("   See the main README.md for more details.");
            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.Write("Have you already configured model deployments? (y/n): ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            if (answer != "y" && answer != "yes")
            {
                Console.WriteLine();
                Console.WriteLine("Please run the ModelDeploymentSetup sample first and then try again.");
                return;
            }
            Console.WriteLine();

            var service = host.Services.GetService<IClassifierService>()!;
            var filePath = "mixed_financial_docs.pdf";

            // Generate unique IDs
            var classifierId = $"classifier_sample_{Guid.NewGuid():N}";
            var loanAnalyzerId = $"loan_analyzer_{Guid.NewGuid():N}";
            var enhancedClassifierId = $"enhanced_classifier_{Guid.NewGuid():N}";

            Console.WriteLine("Creating basic classifier...");

            // Create basic classifier as an analyzer with contentCategories (matching Python implementation)
            var basicClassifierAnalyzer = new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-document",
                ["description"] = $"Custom classifier for classification demo: {classifierId}",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["enableSegment"] = true,
                    ["contentCategories"] = new Dictionary<string, object>
                    {
                        ["Loan application"] = new Dictionary<string, object>
                        {
                            ["description"] = "Documents submitted by individuals or businesses to request funding, typically including personal or business details, financial history, loan amount, purpose, and supporting documentation."
                        },
                        ["Invoice"] = new Dictionary<string, object>
                        {
                            ["description"] = "Billing documents issued by sellers or service providers to request payment for goods or services, detailing items, prices, taxes, totals, and payment terms."
                        },
                        ["Bank_Statement"] = new Dictionary<string, object>
                        {
                            ["description"] = "Official statements issued by banks that summarize account activity over a period, including deposits, withdrawals, fees, and balances."
                        }
                    }
                },
                ["models"] = new Dictionary<string, string>
                {
                    ["completion"] = "gpt-4.1"
                },
                ["tags"] = new Dictionary<string, string>
                {
                    ["demo_type"] = "classification"
                }
            };

            // Classify a document using the basic classifier
            await service.ClassifyDocumentAsync(classifierId, basicClassifierAnalyzer, filePath);

            Console.WriteLine();
            Console.WriteLine("Creating custom loan analyzer...");

            // Create custom loan analyzer
            await service.CreateLoanAnalyzerAsync(loanAnalyzerId);

            Console.WriteLine();
            Console.WriteLine("Creating enhanced classifier with custom analyzer...");

            // Create enhanced classifier as an analyzer with contentCategories and custom analyzer (matching Python implementation)
            var enhancedClassifierAnalyzer = new Dictionary<string, object>
            {
                ["baseAnalyzerId"] = "prebuilt-document",
                ["description"] = $"Enhanced classifier with custom loan analyzer: {enhancedClassifierId}",
                ["config"] = new Dictionary<string, object>
                {
                    ["returnDetails"] = true,
                    ["enableSegment"] = true,
                    ["contentCategories"] = new Dictionary<string, object>
                    {
                        ["Loan application"] = new Dictionary<string, object>
                        {
                            ["description"] = "Documents submitted by individuals or businesses to request funding, typically including personal or business details, financial history, loan amount, purpose, and supporting documentation.",
                            ["analyzerId"] = loanAnalyzerId
                        },
                        ["Invoice"] = new Dictionary<string, object>
                        {
                            ["description"] = "Billing documents issued by sellers or service providers to request payment for goods or services, detailing items, prices, taxes, totals, and payment terms."
                        },
                        ["Bank_Statement"] = new Dictionary<string, object>
                        {
                            ["description"] = "Official statements issued by banks that summarize account activity over a period, including deposits, withdrawals, fees, and balances."
                        }
                    }
                },
                ["models"] = new Dictionary<string, string>
                {
                    ["completion"] = "gpt-4.1"
                },
                ["tags"] = new Dictionary<string, string>
                {
                    ["demo_type"] = "enhanced_classification"
                }
            };

            // Classify a document using the enhanced classifier
            await service.ClassifyDocumentAsync(enhancedClassifierId, enhancedClassifierAnalyzer, filePath);

            // Clean up the custom analyzer
            Console.WriteLine();
            Console.WriteLine("Cleaning up...");
            await service.DeleteAnalyzerAsync(loanAnalyzerId);

            Console.WriteLine();
            Console.WriteLine("Sample completed successfully!");
        }
    }
}
