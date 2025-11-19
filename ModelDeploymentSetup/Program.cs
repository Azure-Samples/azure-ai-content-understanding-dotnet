using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace ModelDeploymentSetup
{
    /// <summary>
    /// Standalone sample for configuring model deployments in Azure AI Content Understanding.
    /// This must be run once before running any other samples that use prebuilt analyzers.
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("Azure AI Content Understanding - Model Deployment Setup");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("This sample configures the default model deployments required for");
            Console.WriteLine("prebuilt analyzers (prebuilt-documentSearch, prebuilt-audioSearch,");
            Console.WriteLine("prebuilt-videoSearch).");
            Console.WriteLine();
            Console.WriteLine("Required deployments:");
            Console.WriteLine("  - GPT-4.1");
            Console.WriteLine("  - GPT-4.1-mini");
            Console.WriteLine("  - text-embedding-3-large");
            Console.WriteLine();
            Console.WriteLine("Make sure you have:");
            Console.WriteLine("  1. Deployed all three models in Azure AI Foundry");
            Console.WriteLine("  2. Configured your deployment names in appsettings.json");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue or Ctrl+C to exit...");
            Console.ReadKey();
            Console.WriteLine();

            // Create host and configure services
            var host = ContentUnderstandingBootstrapper.CreateHost();

            // Get the client from DI
            var client = host.Services.GetRequiredService<AzureContentUnderstandingClient>();
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            // Configure model deployments
            var deploymentConfig = new ModelDeploymentConfiguration(client, configuration);
            
            try
            {
                bool configured = await deploymentConfig.ConfigureDefaultModelDeploymentsAsync();
                Console.WriteLine();

                if (configured)
                {
                    Console.WriteLine("=".PadRight(80, '='));
                    Console.WriteLine("Model deployment configuration completed successfully!");
                    Console.WriteLine("=".PadRight(80, '='));
                    Console.WriteLine();
                    Console.WriteLine("You can now run other samples that use prebuilt analyzers.");
                    Console.WriteLine("The configuration is persisted in your Azure AI Foundry resource.");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("=".PadRight(80, '='));
                    Console.WriteLine("❌ Model deployment configuration failed.");
                    Console.WriteLine("=".PadRight(80, '='));
                    Console.WriteLine();
                    Console.WriteLine("Please:");
                    Console.WriteLine("  1. Ensure all three models are deployed in Azure AI Foundry");
                    Console.WriteLine("  2. Update appsettings.json with your deployment names:");
                    Console.WriteLine("     GPT_4_1_DEPLOYMENT=<your-gpt-4.1-deployment-name>");
                    Console.WriteLine("     GPT_4_1_MINI_DEPLOYMENT=<your-gpt-4.1-mini-deployment-name>");
                    Console.WriteLine("     TEXT_EMBEDDING_3_LARGE_DEPLOYMENT=<your-text-embedding-3-large-deployment-name>");
                    Console.WriteLine("  3. Run this sample again");
                    Console.WriteLine();
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("❌ Error during model deployment configuration");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Common issues:");
                Console.WriteLine("  - Deployment names don't exist in your Azure AI Foundry project");
                Console.WriteLine("  - Insufficient permissions to update defaults");
                Console.WriteLine("  - Network connectivity issues");
                Console.WriteLine();
                Environment.Exit(1);
            }
        }
    }
}

