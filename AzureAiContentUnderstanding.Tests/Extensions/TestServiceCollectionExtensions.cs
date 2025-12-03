using Azure;
using Azure.Core;
using Azure.Identity;
using AzureAiContentUnderstanding.Tests.Recording;
using ContentUnderstanding.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AzureAiContentUnderstanding.Tests.Extensions
{
    /// <summary>
    /// Extensions for configuring services with test recording support.
    /// </summary>
    public static class TestServiceCollectionExtensions
    {
        /// <summary>
        /// Adds AzureContentUnderstandingClient with HTTP recording support for tests.
        /// </summary>
        public static IServiceCollection AddContentUnderstandingClientWithRecording(
            this IServiceCollection services,
            IConfiguration configuration,
            TestRecording? recording = null,
            RecordedTestMode mode = RecordedTestMode.Live)
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
            string? userAgent = null;
            var configValue = configuration["AZURE_AI_USER_AGENT"];
            if (configValue != null)
            {
                userAgent = string.IsNullOrWhiteSpace(configValue) ? null : configValue;
            }
            else
            {
                var envValue = Environment.GetEnvironmentVariable("AZURE_AI_USER_AGENT");
                if (envValue != null)
                {
                    userAgent = string.IsNullOrWhiteSpace(envValue) ? null : envValue;
                }
                else
                {
                    userAgent = "azure-ai-content-understanding-dotnet-sample-ga";
                }
            }

            // Configure ContentUnderstandingOptions
            services.Configure<ContentUnderstandingOptions>(options =>
            {
                options.Endpoint = endpoint;
                options.SubscriptionKey = apiKey ?? "";
                options.ApiVersion = apiVersion;
                options.UserAgent = userAgent;
            });

            // Register HttpClient with recording support
            if (recording != null && mode != RecordedTestMode.Live)
            {
                services.AddHttpClient<AzureContentUnderstandingClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new RecordedHttpMessageHandler(mode, recording));
            }
            else
            {
                services.AddHttpClient<AzureContentUnderstandingClient>();
            }

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
                    Console.WriteLine($"Test client created [{mode} mode]");
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

                    Console.WriteLine("❌ Failed to create test client");
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
