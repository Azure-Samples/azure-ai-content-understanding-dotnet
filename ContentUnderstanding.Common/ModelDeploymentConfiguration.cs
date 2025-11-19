using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ContentUnderstanding.Common
{
    /// <summary>
    /// Helper class for configuring model deployment mappings for Azure AI Content Understanding.
    /// </summary>
    public class ModelDeploymentConfiguration
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly IConfiguration? _configuration;

        public ModelDeploymentConfiguration(AzureContentUnderstandingClient client, IConfiguration? configuration = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _configuration = configuration;
        }

        /// <summary>
        /// Gets a configuration value, checking appsettings.json first, then falling back to environment variables.
        /// This matches the behavior of Python's dotenv where file-based config takes priority.
        /// </summary>
        private static string? GetConfigValue(IConfiguration? configuration, string key)
        {
            // Check appsettings.json first (like Python dotenv)
            if (configuration != null)
            {
                var value = configuration.GetValue<string>(key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            
            // Fallback to environment variable
            return Environment.GetEnvironmentVariable(key);
        }

        /// <summary>
        /// Configure default model deployments for prebuilt analyzers.
        /// </summary>
        /// <returns>True if configuration was successful or skipped, false if configuration failed.</returns>
        public async Task<bool> ConfigureDefaultModelDeploymentsAsync()
        {
            // Get model deployment names from appsettings.json first, then environment variables
            string? gpt41Deployment = GetConfigValue(_configuration, "GPT_4_1_DEPLOYMENT");
            string? gpt41MiniDeployment = GetConfigValue(_configuration, "GPT_4_1_MINI_DEPLOYMENT");
            string? textEmbedding3LargeDeployment = GetConfigValue(_configuration, "TEXT_EMBEDDING_3_LARGE_DEPLOYMENT");

            // Check if required deployments are configured
            var missingDeployments = new List<string>();

            if (string.IsNullOrEmpty(gpt41Deployment))
            {
                missingDeployments.Add("GPT_4_1_DEPLOYMENT");
            }

            if (string.IsNullOrEmpty(gpt41MiniDeployment))
            {
                missingDeployments.Add("GPT_4_1_MINI_DEPLOYMENT");
            }

            if (string.IsNullOrEmpty(textEmbedding3LargeDeployment))
            {
                missingDeployments.Add("TEXT_EMBEDDING_3_LARGE_DEPLOYMENT");
            }

            if (missingDeployments.Any())
            {
                Console.WriteLine("⚠️  Warning: Missing required model deployment configuration(s):");
                foreach (var deployment in missingDeployments)
                {
                    Console.WriteLine($"   - {deployment}");
                }
                Console.WriteLine();
                Console.WriteLine("   Prebuilt analyzers require GPT-4.1, GPT-4.1-mini, and text-embedding-3-large deployments.");
                Console.WriteLine("   Please:");
                Console.WriteLine("   1. Deploy all three models in Azure AI Foundry");
                Console.WriteLine("   2. Set environment variables or add to appsettings.json:");
                Console.WriteLine("      GPT_4_1_DEPLOYMENT=<your-gpt-4.1-deployment-name>");
                Console.WriteLine("      GPT_4_1_MINI_DEPLOYMENT=<your-gpt-4.1-mini-deployment-name>");
                Console.WriteLine("      TEXT_EMBEDDING_3_LARGE_DEPLOYMENT=<your-text-embedding-3-large-deployment-name>");
                Console.WriteLine("   3. Restart the application");

                return false;
            }

            Console.WriteLine("📋 Configuring default model deployments...");
            Console.WriteLine($"   GPT-4.1 deployment: {gpt41Deployment}");
            Console.WriteLine($"   GPT-4.1-mini deployment: {gpt41MiniDeployment}");
            Console.WriteLine($"   text-embedding-3-large deployment: {textEmbedding3LargeDeployment}");

            try
            {
                // Update defaults to map model names to your deployments
                var result = await _client.UpdateDefaultsAsync(new Dictionary<string, string?>
                {
                    ["gpt-4.1"] = gpt41Deployment,
                    ["gpt-4.1-mini"] = gpt41MiniDeployment,
                    ["text-embedding-3-large"] = textEmbedding3LargeDeployment
                });

                Console.WriteLine("✅ Default model deployments configured successfully");
                Console.WriteLine("   Model mappings:");

                if (result.ContainsKey("modelDeployments"))
                {
                    var modelDeploymentsJson = result["modelDeployments"].ToString();
                    if (modelDeploymentsJson != null)
                    {
                        var modelDeployments = JsonSerializer.Deserialize<Dictionary<string, string>>(modelDeploymentsJson);
                        if (modelDeployments != null)
                        {
                            foreach (var (model, deployment) in modelDeployments)
                            {
                                Console.WriteLine($"     {model} → {deployment}");
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to configure defaults: {ex.Message}");
                Console.WriteLine("   This may happen if:");
                Console.WriteLine("   - One or more deployment names don't exist in your Azure AI Foundry project");
                Console.WriteLine("   - You don't have permission to update defaults");
                throw;
            }
        }

        /// <summary>
        /// Validate that required model deployments are configured in environment variables or configuration.
        /// </summary>
        /// <param name="configuration">Optional configuration to read from appsettings.json</param>
        /// <returns>True if all required deployments are configured, false otherwise.</returns>
        public static bool ValidateDeploymentConfiguration(IConfiguration? configuration = null)
        {
            string? gpt41Deployment = GetConfigValue(configuration, "GPT_4_1_DEPLOYMENT");
            string? gpt41MiniDeployment = GetConfigValue(configuration, "GPT_4_1_MINI_DEPLOYMENT");
            string? textEmbedding3LargeDeployment = GetConfigValue(configuration, "TEXT_EMBEDDING_3_LARGE_DEPLOYMENT");

            return !string.IsNullOrEmpty(gpt41Deployment)
                && !string.IsNullOrEmpty(gpt41MiniDeployment)
                && !string.IsNullOrEmpty(textEmbedding3LargeDeployment);
        }

        /// <summary>
        /// Get the list of missing deployment configurations.
        /// </summary>
        /// <param name="configuration">Optional configuration to read from appsettings.json</param>
        /// <returns>List of missing deployment environment variable names.</returns>
        public static List<string> GetMissingDeployments(IConfiguration? configuration = null)
        {
            var missingDeployments = new List<string>();

            if (string.IsNullOrEmpty(GetConfigValue(configuration, "GPT_4_1_DEPLOYMENT")))
            {
                missingDeployments.Add("GPT_4_1_DEPLOYMENT");
            }

            if (string.IsNullOrEmpty(GetConfigValue(configuration, "GPT_4_1_MINI_DEPLOYMENT")))
            {
                missingDeployments.Add("GPT_4_1_MINI_DEPLOYMENT");
            }

            if (string.IsNullOrEmpty(GetConfigValue(configuration, "TEXT_EMBEDDING_3_LARGE_DEPLOYMENT")))
            {
                missingDeployments.Add("TEXT_EMBEDDING_3_LARGE_DEPLOYMENT");
            }

            return missingDeployments;
        }
    }
}
