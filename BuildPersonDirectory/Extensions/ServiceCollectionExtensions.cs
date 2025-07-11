using Azure.Core;
using Azure.Identity;
using ContentUnderstanding.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildPersonDirectory.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddConfigurations(this IServiceCollection services, Action<ContentUnderstandingOptions> options)
        {
            services.Configure(options);

            return services;
        }
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
        public static IServiceCollection AddHttpClientRequest(this IServiceCollection services)
        {
            services.AddHttpClient<AzureContentUnderstandingFaceClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ContentUnderstandingOptions>>().Value;
                client.BaseAddress = new Uri(options.Endpoint);
                client.DefaultRequestHeaders.Add("x-ms-useragent", options.UserAgent);

                if (!string.IsNullOrEmpty(options.SubscriptionKey))
                {
                    client.DefaultRequestHeaders.Add("Apim-Subscription-id", options.SubscriptionKey);
                }
            });

            return services;
        }
    }
}
