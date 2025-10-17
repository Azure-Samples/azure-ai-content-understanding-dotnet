using Azure;
using Azure.AI.ContentUnderstanding;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentUnderstanding.Common.Extensions
{
    /// <summary>
    /// Provides extension methods for configuring services in an <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>These methods simplify the registration of common services and configurations, such as
    /// content understanding options and token providers, into the dependency injection container.</remarks>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddContentUnderstandingClient(this IServiceCollection services, IConfiguration configuration)
        {
            // Read `endpoint` and `key` from environment variables or configuration
            string endpoint = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") 
                ?? configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_ENDPOINT") 
                ?? throw new InvalidOperationException("AZURE_CONTENT_UNDERSTANDING_ENDPOINT environment variable is not set.");

            string key = Environment.GetEnvironmentVariable("AZURE_CONTENT_UNDERSTANDING_KEY") 
                ?? configuration.GetValue<string>("AZURE_CONTENT_UNDERSTANDING_KEY") 
                ?? throw new InvalidOperationException("AZURE_CONTENT_UNDERSTANDING_KEY environment variable is not set.");

            // Register ContentUnderstandingClient with the appropriate credentials
            services.AddSingleton(provider =>
            {
                if (!string.IsNullOrEmpty(key))
                {
                    return new ContentUnderstandingClient(new Uri(endpoint), new AzureKeyCredential(key));
                }
                else
                {
                    // Use DefaultAzureCredential for local development (works with 'az login')
                    return new ContentUnderstandingClient(new Uri(endpoint), new AzureCliCredential());
                }
            });

            return services;
        }

        #region Need to delete
        /// <summary>
        /// Adds configuration settings for content understanding to the specified service collection.
        /// </summary>
        /// <remarks>This method allows you to configure content understanding options by providing a
        /// delegate that sets the desired properties on a <see cref="ContentUnderstandingOptions"/> instance.</remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to which the configurations will be added.</param>
        /// <param name="options">An action delegate to configure the <see cref="ContentUnderstandingOptions"/>.</param>
        /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
        public static IServiceCollection AddConfigurations(this IServiceCollection services, Action<ContentUnderstandingOptions> options)
        {
            services.Configure(options);

            return services;
        }

        /// <summary>
        /// Adds a token provider to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <remarks>This method registers a <see cref="TokenCredential"/> implementation and a delegate
        /// for obtaining an access token asynchronously. The token provider is configured to request tokens for the
        /// "https://cognitiveservices.azure.com/.default" scope.</remarks>
        /// <param name="services">The <see cref="IServiceCollection"/> to which the token provider is added.</param>
        /// <returns>The updated <see cref="IServiceCollection"/> instance, allowing for method chaining.</returns>
        public static IServiceCollection AddTokenProvider(this IServiceCollection services)
        {
            services.AddSingleton<TokenCredential, DefaultAzureCredential>();

            services.AddSingleton<Func<Task<string>>>(provider =>
            {
                var credential = provider.GetRequiredService<TokenCredential>();
                return async () =>
                {
                    var tokenRequestContext = new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" });
                    var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
                    return token.Token;
                };
            });

            return services;
        }

        #endregion
    }
}
