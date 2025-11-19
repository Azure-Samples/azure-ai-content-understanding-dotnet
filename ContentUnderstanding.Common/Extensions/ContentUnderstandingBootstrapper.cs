using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContentUnderstanding.Common.Extensions
{
    /// <summary>
    /// Provides bootstrapping functionality for Azure AI Content Understanding applications.
    /// Handles client initialization and model deployment configuration.
    /// </summary>
    public static class ContentUnderstandingBootstrapper
    {
        /// <summary>
        /// Initialize and configure Azure AI Content Understanding with model deployments.
        /// </summary>
        /// <param name="host">The configured host.</param>
        /// <param name="skipDeploymentConfiguration">Skip model deployment configuration if true. Default is false.</param>
        /// <param name="exitOnConfigurationFailure">Exit application if deployment configuration fails. Default is false.</param>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        public static async Task<bool> InitializeAsync(
            IHost host,
            bool skipDeploymentConfiguration = false,
            bool exitOnConfigurationFailure = false)
        {
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("Initializing Azure AI Content Understanding");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();

            // Get the client from DI
            var client = host.Services.GetService<AzureContentUnderstandingClient>();
            if (client == null)
            {
                Console.WriteLine("❌ Failed to resolve AzureContentUnderstandingClient from DI container.");
                Console.WriteLine("   Please ensure AddContentUnderstandingClient() is called in ConfigureServices.");
                return false;
            }

            // Skip deployment configuration if requested
            if (skipDeploymentConfiguration)
            {
                Console.WriteLine("⚠️  Skipping model deployment configuration (skipDeploymentConfiguration = true)");
                Console.WriteLine();
                return true;
            }

            // Configure model deployments
            var deploymentConfig = new ModelDeploymentConfiguration(client);
            bool configured = await deploymentConfig.ConfigureDefaultModelDeploymentsAsync();
            Console.WriteLine();

            if (!configured)
            {
                Console.WriteLine("⚠️  Warning: Model deployments are not configured.");
                Console.WriteLine("   Some features (prebuilt analyzers) may not work correctly.");
                Console.WriteLine();

                if (exitOnConfigurationFailure)
                {
                    Console.WriteLine("Application terminated due to missing deployment configuration.");
                    Console.WriteLine("Please configure the required environment variables and try again.");
                    return false;
                }

                Console.Write("Do you want to continue anyway? (y/n): ");
                var answer = Console.ReadLine()?.Trim().ToLower();
                if (answer != "y" && answer != "yes")
                {
                    Console.WriteLine("Application terminated. Please configure deployments and try again.");
                    return false;
                }
                Console.WriteLine();
            }

            return true;
        }

        /// <summary>
        /// Validate deployment configuration without configuring the client.
        /// </summary>
        /// <param name="showDetails">Show detailed information about missing deployments. Default is true.</param>
        /// <returns>True if all required deployments are configured, false otherwise.</returns>
        public static bool ValidateConfiguration(bool showDetails = true)
        {
            bool isValid = ModelDeploymentConfiguration.ValidateDeploymentConfiguration();

            if (!isValid && showDetails)
            {
                var missingDeployments = ModelDeploymentConfiguration.GetMissingDeployments();
                Console.WriteLine("⚠️  Warning: Missing required model deployment configuration(s):");
                foreach (var deployment in missingDeployments)
                {
                    Console.WriteLine($"   - {deployment}");
                }
                Console.WriteLine();
                Console.WriteLine("   Please set the following environment variables:");
                Console.WriteLine("   - GPT_4_1_DEPLOYMENT");
                Console.WriteLine("   - GPT_4_1_MINI_DEPLOYMENT");
                Console.WriteLine("   - TEXT_EMBEDDING_3_LARGE_DEPLOYMENT");
                Console.WriteLine();
            }

            return isValid;
        }

        /// <summary>
        /// Create and configure a minimal host with Azure Content Understanding client.
        /// This is a convenience method for simple console applications.
        /// </summary>
        /// <param name="configureServices">Additional service configuration action.</param>
        /// <returns>Configured host instance.</returns>
        public static IHost CreateHost(Action<HostBuilderContext, IServiceCollection>? configureServices = null)
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Add Content Understanding Client
                    services.AddContentUnderstandingClient(context.Configuration);

                    // Add custom services
                    configureServices?.Invoke(context, services);
                });

            return builder.Build();
        }

        /// <summary>
        /// Full bootstrap: create host, initialize, and return services.
        /// </summary>
        /// <param name="configureServices">Additional service configuration action.</param>
        /// <param name="skipDeploymentConfiguration">Skip model deployment configuration if true.</param>
        /// <param name="exitOnConfigurationFailure">Exit if deployment configuration fails.</param>
        /// <returns>Service provider if successful, null otherwise.</returns>
        public static async Task<IServiceProvider?> BootstrapAsync(
            Action<HostBuilderContext, IServiceCollection>? configureServices = null,
            bool skipDeploymentConfiguration = false,
            bool exitOnConfigurationFailure = false)
        {
            // Create host
            var host = CreateHost(configureServices);

            // Initialize
            bool initialized = await InitializeAsync(
                host,
                skipDeploymentConfiguration,
                exitOnConfigurationFailure);

            if (!initialized)
            {
                return null;
            }

            return host.Services;
        }
    }
}
