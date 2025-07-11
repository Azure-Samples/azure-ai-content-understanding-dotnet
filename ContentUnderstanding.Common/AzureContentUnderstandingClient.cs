using ContentUnderstanding.Common.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ContentUnderstanding.Common
{
    /// <summary>
    /// AzureContentUnderstandingClient
    /// </summary>
    public class AzureContentUnderstandingClient
    {
        public const string OPERATION_LOCATION = "Operation-Location";
        private readonly HttpClient _httpClient;
        private readonly IOptions<ContentUnderstandingOptions> _options;
        private readonly Func<Task<string>> _tokenProvider;

        public AzureContentUnderstandingClient(HttpClient httpClient, IOptions<ContentUnderstandingOptions> options, Func<Task<string>> tokenProvider)
        {
            _httpClient = httpClient;
            _options = options;
            _tokenProvider = tokenProvider;
        }

        private string GetAnalyzerUrl(string analyzerId) =>
            $"{_options.Value.Endpoint}/contentunderstanding/analyzers/{analyzerId}?api-version={_options.Value.ApiVersion}";

        private string GetAnalyzerListUrl() =>
            $"{_options.Value.Endpoint}/contentunderstanding/analyzers?api-version={_options.Value.ApiVersion}";

        private string GetAnalyzeUrl(string analyzerId) =>
            $"{_options.Value.Endpoint}/contentunderstanding/analyzers/{analyzerId}:analyze?api-version={_options.Value.ApiVersion}";

        private Dictionary<string, string> GetTrainingDataConfig(string storageContainerSasUrl, string storageContainerPathPrefix) =>
            new Dictionary<string, string>
            {
                ["containerUrl"] = storageContainerSasUrl,
                ["kind"] = "blob",
                ["prefix"] = storageContainerPathPrefix
            };
        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, HttpContent content = null)
        {
            var request = new HttpRequestMessage(method, url);

            // add authorization
            if (!string.IsNullOrEmpty(_options.Value.SubscriptionKey))
            {
                request.Headers.Add("Apim-Subscription-id", _options.Value.SubscriptionKey);
            }
            else if (_tokenProvider != null)
            {
                var token = await _tokenProvider().ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            request.Headers.Add("x-ms-useragent", _options.Value.UserAgent);
            request.Content = content;
            return request;
        }

        public async Task<AnalyzerInfo[]> GetAllAnalyzersAsync()
        {
            var url = GetAnalyzerListUrl();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokenProvider());

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AnalyzerListResponse>(content)?.Value ?? Array.Empty<AnalyzerInfo>();
        }

        /// <summary>
        /// Retrieves a specific analyzer detail through analyzerid from the content understanding service.
        /// This method sends a GET request to the service endpoint to get the analyzer detail.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer.</param>
        /// <returns>A dictionary containing the JSON response from the service, which includes the target analyzer detail.</returns>
        public async Task<AnalyzerDetail?> GetAnalyzerDetailByIdAsync(string analyzerId)
        {
            var url = $"{_options.Value.Endpoint}/contentunderstanding/analyzers/{analyzerId}?api-version={_options.Value.ApiVersion}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokenProvider());

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            
            return JsonSerializer.Deserialize<AnalyzerDetail>(content);
        }

        /// <summary>
        /// Initiates the creation of an analyzer with the given ID and schema.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer.</param>
        /// <param name="analyzerTemplate">The schema definition for the analyzer. Defaults to None.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer schema JSON file. Defaults to "".</param>
        /// <param name="trainingStorageContainerSasUrl">The SAS URL for the training storage container. Defaults to "".</param>
        /// <param name="trainingStorageContainerPathPrefix">The path prefix within the training storage container. Defaults to "".</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The response object from the HTTP request.</returns>
        /// <exception cref="ArgumentException">If neither `analyzerTemplate` nor `analyzerTemplatePath` is provided.</exception>
        public async Task<HttpResponseMessage> BeginCreateAnalyzerAsync(
            string analyzerId,
            JsonDocument? analyzerTemplate = null,
            string analyzerTemplatePath = "",
            string trainingStorageContainerSasUrl = "",
            string trainingStorageContainerPathPrefix = "",
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(analyzerTemplatePath) && File.Exists(analyzerTemplatePath))
            {
                var jsonString = await File.ReadAllTextAsync(analyzerTemplatePath, cancellationToken).ConfigureAwait(false);
                analyzerTemplate = JsonDocument.Parse(jsonString);
            }

            if (analyzerTemplate == null)
                throw new ArgumentException("Analyzer schema must be provided either through analyzerTemplate or analyzerTemplatePath");

            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(
                analyzerTemplate.RootElement.GetRawText());

            if (jsonObject != null && !string.IsNullOrEmpty(trainingStorageContainerSasUrl) && !string.IsNullOrEmpty(trainingStorageContainerPathPrefix))
            {
                var trainingConfig = GetTrainingDataConfig(trainingStorageContainerSasUrl, trainingStorageContainerPathPrefix);
                jsonObject["trainingData"] = trainingConfig;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                GetAnalyzerUrl(analyzerId))
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(jsonObject),
                    Encoding.UTF8,
                    "application/json")
            };

            await EnsureAuthorizationAsync(request).ConfigureAwait(false);

            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return response;
        }

        /// <summary>
        /// Deletes an analyzer with the specified analyzer ID.
        /// </summary>
        /// <param name="analyzerId">The ID of the analyzer to be deleted.</param>
        /// <returns>The response object from the delete request.</returns>
        public async Task<HttpResponseMessage> DeleteAnalyzerAsync(string analyzerId)
        {
            var url = GetAnalyzerUrl(analyzerId);
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokenProvider());

            var response = await _httpClient.SendAsync(request);
            return response;
        }

        /// <summary>
        /// Begins the analysis of a file or URL using the specified analyzer.
        /// </summary>
        /// <param name="analyzerId">The ID of the analyzer to use.</param>
        /// <param name="fileLocation">The path to the file or the URL to analyze.</param>
        /// <returns>The response from the analysis request.</returns>
        public async Task<HttpResponseMessage> BeginAnalyzeAsync(string analyzerId, string fileLocation, string apiNameDescription = "")
        {
            if (string.IsNullOrEmpty(analyzerId)) throw new ArgumentNullException("Parameters 'analyzerId' can't be null or empty.");

            HttpContent content;
            if (File.Exists(fileLocation))
            {
                var bytes = await File.ReadAllBytesAsync(fileLocation).ConfigureAwait(false);
                content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            }
            else if (Uri.IsWellFormedUriString(fileLocation, UriKind.Absolute))
            {
                var data = new Dictionary<string, string> { ["url"] = fileLocation };
                content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            }
            else
            {
                throw new ArgumentException("File location must be a valid path or URL.");
            }

            var url = GetAnalyzeUrl(analyzerId);
            var request = await CreateRequestAsync(HttpMethod.Post, url, content).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (!string.IsNullOrWhiteSpace(apiNameDescription))
            {
                Console.WriteLine($"Use {analyzerId} to {apiNameDescription} from the file: {fileLocation}");
            }

            return response;
        }

        /// <summary>
        /// Retrieves an image from the analyze operation using the image ID.
        /// </summary>
        /// <param name="analyzeResponse">The response object from the analyze operation.</param>
        /// <param name="imageId">The ID of the image to retrieve.</param>
        /// <returns>The image content as a byte string.</returns>
        public async Task<byte[]> GetImageFromAnalyzeOperationAsync(HttpResponseMessage analyzeResponse, string imageId)
        {
            if (!analyzeResponse.Headers.TryGetValues(OPERATION_LOCATION, out var locations))
                throw new KeyNotFoundException("Operation location not found in response headers.");

            var operationLocation = locations?.First().Split("?api-version")[0];
            if (string.IsNullOrEmpty(operationLocation))
                throw new ArgumentException("Invalid operation location header.");

            var imageUrl = $"{operationLocation}/files/{imageId}?api-version={_options.Value.ApiVersion}";
            var request = await CreateRequestAsync(HttpMethod.Get, imageUrl).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            if (response.Content.Headers.ContentType?.MediaType != "image/jpeg")
                throw new InvalidDataException("Unexpected content type in response.");

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Polls the result of an asynchronous operation until it completes or times out.
        /// </summary>
        /// <param name="initialResponse">The initial response object containing the operation location.</param>
        /// <param name="timeoutSeconds">The maximum number of seconds to wait for the operation to complete. Defaults to 120.</param>
        /// <param name="pollingIntervalSeconds">The number of seconds to wait between polling attempts. Defaults to 2.</param>
        /// <returns>The JSON response of the completed operation if it succeeds.</returns>
        public async Task<JsonDocument> PollResultAsync(
            HttpResponseMessage initialResponse,
            int timeoutSeconds = 120,
            int pollingIntervalSeconds = 2)
        {
            if (!initialResponse.Headers.TryGetValues(OPERATION_LOCATION, out var locations))
                throw new KeyNotFoundException("Operation location not found in response headers.");

            var operationLocation = locations.First();
            if (string.IsNullOrEmpty(operationLocation))
                throw new ArgumentException("Invalid operation location header.");

            using var cts = new CancellationTokenSource(timeoutSeconds * 1000);
            var startTime = DateTime.UtcNow;

            while (!cts.IsCancellationRequested)
            {
                var request = await CreateRequestAsync(HttpMethod.Get, operationLocation).ConfigureAwait(false);
                var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync().ConfigureAwait(false));

                var status = json.RootElement.GetProperty("status").GetString()?.ToLower();
                switch (status)
                {
                    case "succeeded":
                        return json;
                    case "failed":
                        throw new ApplicationException($"Request failed: {json.RootElement}");
                    default:
                        // wait
                        await Task.Delay(pollingIntervalSeconds * 1000, cts.Token).ConfigureAwait(false);
                        break;
                }
            }

            throw new TimeoutException(
                $"Operation timed out after {timeoutSeconds} seconds.");
        }

        /// <summary>
        /// Http request authorization helper method.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="AuthException"></exception>
        private async Task EnsureAuthorizationAsync(HttpRequestMessage request)
        {
            try
            {
                var token = await _tokenProvider().ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            catch (Exception ex)
            {
                throw new AuthException("Failed to get authorization token", ex);
            }
        }

        /// <summary>
        /// AuthException is thrown when there is an issue with authentication, such as failing to retrieve a token.
        /// </summary>
        public class AuthException : Exception
        {
            public AuthException(string message, Exception inner) : base(message, inner) { }
        }

        /// <summary>
        /// InvalidContentTypeException is thrown when the content type of a response does not match the expected type.
        /// </summary>
        public class InvalidContentTypeException : Exception
        {
            public InvalidContentTypeException(string message) : base(message) { }
        }

        /// <summary>
        /// OperationFailedException is thrown when an operation fails, such as when an analysis request does not succeed.
        /// </summary>
        public class OperationFailedException : Exception
        {
            public OperationFailedException(string message) : base(message) { }
        }
    }
}
