using Azure.Core;
using Azure.Identity;

namespace ContentUnderstanding.Common
{
    public class AzureTokenProvider
    {
        private readonly DefaultAzureCredential _credential;
        private const string TokenScope = "https://cognitiveservices.azure.com/.default";

        public AzureTokenProvider(DefaultAzureCredential credential)
        {
            _credential = credential;
        }

        public async Task<string> GetTokenAsync()
        {
            var tokenResult = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { TokenScope }),
                CancellationToken.None);
            return tokenResult.Token;
        }
    }
}
