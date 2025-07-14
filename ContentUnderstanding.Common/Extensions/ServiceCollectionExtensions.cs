using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common.Extensions
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
