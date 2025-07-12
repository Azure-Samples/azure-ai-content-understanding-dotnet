using Azure.Core;
using Azure.Identity;
using ContentUnderstanding.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentExtraction.Extensions
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
    }
}
