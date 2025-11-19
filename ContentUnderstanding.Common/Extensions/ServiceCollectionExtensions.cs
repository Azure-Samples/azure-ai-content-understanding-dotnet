using Azure;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace ContentUnderstanding.Common.Extensions
{
    /// <summary>
    /// Provides extension methods for configuring services in an <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>These methods simplify the registration of the AzureContentUnderstandingClient
    /// into the dependency injection container with automatic credential resolution.</remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds AzureContentUnderstandingClient to the service collection with automatic credential resolution.
        /// </summary>
        /// <remarks>
        /// This method configures the AzureContentUnderstandingClient by reading configuration from:
        /// 1. Environment variables (AZURE_AI_ENDPOINT, AZURE_AI_API_KEY)
        /// 2. Configuration settings (appsettings.json or similar)
        /// 
        /// Authentication methods (in priority order):
        /// 1. API Key (if AZURE_AI_API_KEY is set) - For development/testing
        /// 2. Azure AD Token (DefaultAzureCredential) - For production (recommended)
        /// 
        /// The API version defaults to "2025-11-01" but can be overridden in configuration.
        /// </remarks>
        /// <param name="services">The service collection to add the client to.</param>
        /// <param name="configuration">The configuration instance to read settings from.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown when AZURE_AI_ENDPOINT is not configured.</exception>
        public static IServiceCollection AddContentUnderstandingClient(
           this IServiceCollection services,
           IConfiguration configuration)
        {
            // Read endpoint from environment variables or configuration
            string endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")
                ?? configuration.GetValue<string>("AZURE_AI_ENDPOINT")
                ?? throw new InvalidOperationException(
                    "AZURE_AI_ENDPOINT is not configured. " +
                    "Please set it in environment variables or appsettings.json");

            // Read API key from environment variables or configuration (optional)
            string? apiKey = Environment.GetEnvironmentVariable("AZURE_AI_API_KEY")
                ?? configuration.GetValue<string>("AZURE_AI_API_KEY");

            // API version
            string apiVersion = Environment.GetEnvironmentVariable("AZURE_AI_API_VERSION")
                ?? configuration.GetValue<string>("AZURE_AI_API_VERSION")
                ?? "2025-11-01";

            // Read user agent from configuration or use default
            string userAgent = configuration.GetValue<string>("AZURE_AI_USER_AGENT")
                ?? "cu-dotnet-client";

            // Configure ContentUnderstandingOptions
            services.Configure<ContentUnderstandingOptions>(options =>
            {
                options.Endpoint = endpoint;
                options.SubscriptionKey = apiKey ?? "";
                options.ApiVersion = apiVersion;
                options.UserAgent = userAgent;
            });

            // Register HttpClient for AzureContentUnderstandingClient
            services.AddHttpClient<AzureContentUnderstandingClient>();

            // Register token provider if no API key is provided
            if (string.IsNullOrEmpty(apiKey))
            {
                services.AddSingleton<TokenCredential, DefaultAzureCredential>();

                services.AddSingleton<Func<Task<string>>>(provider =>
                {
                    var credential = provider.GetRequiredService<TokenCredential>();
                    return async () =>
                    {
                        var tokenRequestContext = new TokenRequestContext(
                            new[] { "https://cognitiveservices.azure.com/.default" });
                        var token = await credential.GetTokenAsync(
                            tokenRequestContext,
                            CancellationToken.None);
                        return token.Token;
                    };
                });
            }
            else
            {
                // Register a null token provider when using API key
                services.AddSingleton<Func<Task<string>>>(_ => null!);
            }

            // Register AzureContentUnderstandingClient
            services.AddSingleton<AzureContentUnderstandingClient>(provider =>
            {
                var httpClient = provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(nameof(AzureContentUnderstandingClient));
                var options = provider.GetRequiredService<IOptions<ContentUnderstandingOptions>>();
                var tokenProvider = provider.GetRequiredService<Func<Task<string>>>();

                string credentialType;

                try
                {
                    var client = new AzureContentUnderstandingClient(
                        httpClient: httpClient,
                        options: options,
                        tokenProvider: tokenProvider
                    );

                    credentialType = !string.IsNullOrEmpty(options.Value.SubscriptionKey)
                        ? "Subscription Key"
                        : "Azure AD Token";

                    // Log successful client creation
                    Console.WriteLine("Client created successfully");
                    Console.WriteLine($"   Endpoint: {options.Value.Endpoint}");
                    Console.WriteLine($"   Credential: {credentialType}");
                    Console.WriteLine($"   API Version: {options.Value.ApiVersion}");

                    return client;
                }
                catch (Exception ex)
                {
                    credentialType = !string.IsNullOrEmpty(options.Value.SubscriptionKey)
                        ? "Subscription Key"
                        : "Azure AD Token";

                    Console.WriteLine("❌ Failed to create client");
                    Console.WriteLine($"   Endpoint: {options.Value.Endpoint}");
                    Console.WriteLine($"   Credential: {credentialType}");
                    Console.WriteLine($"   Error: {ex.Message}");
                    throw;
                }
            });

            return services;
        }
    }
}
