using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
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
        private readonly HashSet<string> SUPPORTED_FILE_TYPES_DOCUMENT = new HashSet<string>
        {
            ".pdf", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".heif"
        };
        private readonly HashSet<string> SUPPORTED_FILE_TYPES_DOCUMENT_TXT = new HashSet<string>
        {
            ".pdf", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".heif", ".docx", ".xlsx", ".pptx", ".txt", ".html", ".md", ".eml", ".msg", ".xml"
        };
        private const string LABEL_FILE_SUFFIX = ".labels.json";
        private const string OCR_RESULT_FILE_SUFFIX = ".result.json";
        private const string KNOWLEDGE_SOURCE_LIST_FILE_NAME = "sources.jsonl";
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AzureContentUnderstandingClient"/> class with the specified
        /// HTTP client, options, and token provider.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> instance used to send requests to the Azure Content Understanding service. This
        /// client should be configured with the necessary base address and any required handlers.</param>
        /// <param name="options">The configuration options for the content understanding service, encapsulated in an <see
        /// cref="IOptions{ContentUnderstandingOptions}"/> object. These options typically include settings such as
        /// endpoint URLs and service-specific parameters.</param>
        /// <param name="tokenProvider">A function that asynchronously provides an authentication token for accessing the Azure Content
        /// Understanding service. This token is required for authorization and should be refreshed as needed.</param>
        public AzureContentUnderstandingClient(HttpClient httpClient, IOptions<ContentUnderstandingOptions> options, Func<Task<string>> tokenProvider)
        {
            _httpClient = httpClient;
            _options = options;
            _tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Constructs the URL for accessing a specific content understanding analyzer.
        /// </summary>
        /// <param name="analyzerId">The unique identifier of the analyzer to access. Cannot be null or empty.</param>
        /// <returns>A string representing the full URL to the specified analyzer, including the API version.</returns>
        private string GetAnalyzerUrl(string analyzerId) =>
            $"{_options.Value.Endpoint}/contentunderstanding/analyzers/{analyzerId}?api-version={_options.Value.ApiVersion}";

        /// <summary>
        /// Constructs the URL for retrieving the list of analyzers from the content understanding service.
        /// </summary>
        /// <returns>A string representing the URL to access the analyzers list, including the endpoint and API version.</returns>
        private string GetAnalyzerListUrl() =>
            $"{_options.Value.Endpoint}/contentunderstanding/analyzers?api-version={_options.Value.ApiVersion}";

        /// <summary>
        /// Constructs the URL for analyzing content using the specified analyzer.
        /// </summary>
        /// <param name="analyzerId">The unique identifier of the analyzer to be used for content analysis.</param>
        /// <returns>A string representing the full URL to invoke the content analysis service with the specified analyzer.</returns>
        private string GetAnalyzeUrl(string analyzerId) =>
            $"{_options.Value.Endpoint}/contentunderstanding/analyzers/{analyzerId}:analyze?api-version={_options.Value.ApiVersion}";

        /// <summary>
        /// Constructs the URL for accessing a specific classifier by its identifier.
        /// </summary>
        /// <param name="classifierId">The unique identifier of the classifier. Cannot be null or empty.</param>
        /// <returns>The fully qualified URL as a string to access the specified classifier.</returns>
        private string GetClassifierUrl(string classifierId) =>
            $"{_options.Value.Endpoint}/contentunderstanding/classifiers/{classifierId}?api-version={_options.Value.ApiVersion}";

        /// <summary>
        /// Constructs the URL for classifying content using a specified classifier.
        /// </summary>
        /// <param name="classifierId">The unique identifier of the classifier to be used for content classification. Cannot be null or empty.</param>
        /// <returns>A string representing the full URL to the classification endpoint, including the specified classifier ID and
        /// API version.</returns>
        private string GetClassifyUrl(string classifierId) =>
            $"{_options.Value.Endpoint}/contentunderstanding/classifiers/{classifierId}:classify?api-version={_options.Value.ApiVersion}";

        /// <summary>
        /// Retrieves the file suffix used for label files.
        /// </summary>
        /// <returns>A string representing the suffix appended to label file names.</returns>
        public string GetLabelFileSuffix() => LABEL_FILE_SUFFIX;

        /// <summary>
        /// Gets the file suffix used for OCR result files.
        /// </summary>
        /// <returns>A string representing the file suffix for OCR result files.</returns>
        public string GetOcrResultFileSuffix() => OCR_RESULT_FILE_SUFFIX;

        /// <summary>
        /// Retrieves a set of supported file types for document processing.
        /// </summary>
        /// <returns>A <see cref="HashSet{T}"/> containing the file extensions of supported document types.</returns>
        public HashSet<string> GetSupportedFileTypesDocument() => SUPPORTED_FILE_TYPES_DOCUMENT;

        /// <summary>
        /// Retrieves the set of supported file types for document text processing.
        /// </summary>
        /// <returns>A <see cref="HashSet{T}"/> containing the file extensions of supported document text types.</returns>
        public HashSet<string> GetSupportedFileTypesDocumentTxt() => SUPPORTED_FILE_TYPES_DOCUMENT_TXT;

        /// <summary>
        /// Gets the file name of the knowledge source list.
        /// </summary>
        /// <returns>The file name of the knowledge source list as a string.</returns>
        public string GetKnowledgeSourceListFileName() => KNOWLEDGE_SOURCE_LIST_FILE_NAME;

        /// <summary>
        /// Retrieves the configuration settings for accessing training data stored in a blob storage container.
        /// </summary>
        /// <param name="storageContainerSasUrl">The SAS URL of the storage container, providing access permissions.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container to locate the training data.</param>
        /// <returns>A dictionary containing configuration keys and their corresponding values for accessing the training data.</returns>
        private Dictionary<string, string> GetTrainingDataConfig(string storageContainerSasUrl, string storageContainerPathPrefix) =>
            new Dictionary<string, string>
            {
                ["containerUrl"] = storageContainerSasUrl,
                ["kind"] = "blob",
                ["prefix"] = storageContainerPathPrefix
            };

        /// <summary>
        /// Retrieves the configuration for Pro Mode reference documents.
        /// </summary>
        /// <param name="storageContainerSasUrl">The SAS URL of the storage container where the reference documents are stored.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container for the reference documents.</param>
        /// <returns>A dictionary containing configuration settings for Pro Mode reference documents,  including the container
        /// URL, document kind, path prefix, and file list path.</returns>
        private List<Dictionary<string, string>> GetProModeReferenceDocsConfig(string storageContainerSasUrl, string storageContainerPathPrefix) => 
            new List<Dictionary<string, string>>
            {
                new()
                {
                    ["containerUrl"] = storageContainerSasUrl,
                    ["kind"] = "reference",
                    ["prefix"] = storageContainerPathPrefix,
                    ["fileListPath"] = KNOWLEDGE_SOURCE_LIST_FILE_NAME
                }
            };

        /// <summary>
        /// Asynchronously creates an <see cref="HttpRequestMessage"/> with the specified HTTP method, URL, and optional
        /// content.
        /// </summary>
        /// <remarks>The request includes authorization headers based on the configuration. If a
        /// subscription key is provided, it is added to the headers. Otherwise, a bearer token is retrieved
        /// asynchronously from the token provider and added to the authorization header.</remarks>
        /// <param name="method">The HTTP method to be used for the request, such as <see cref="HttpMethod.Get"/> or <see
        /// cref="HttpMethod.Post"/>.</param>
        /// <param name="url">The URL to which the request is sent. This must be a valid URI.</param>
        /// <param name="content">The optional HTTP content to be sent with the request. This can be <see langword="null"/> if no content is
        /// needed.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the configured <see
        /// cref="HttpRequestMessage"/>.</returns>
        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, url);

            // add authorization
            if (!string.IsNullOrEmpty(_options.Value.SubscriptionKey))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", _options.Value.SubscriptionKey);
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

        /// <summary>
        /// Asynchronously retrieves a list of all available analyzers.
        /// </summary>
        /// <remarks>This method sends an HTTP GET request to the configured analyzer list URL and returns
        /// the analyzers as a JSON array. The request must succeed for the method to return a result; otherwise, an
        /// exception is thrown.</remarks>
        /// <returns>An array of <see cref="JsonElement"/> representing the analyzers. Returns <see langword="null"/> if no
        /// analyzers are available.</returns>
        public async Task<JsonElement[]?> GetAllAnalyzersAsync()
        {
            var url = GetAnalyzerListUrl();
            var request = await CreateRequestAsync(HttpMethod.Get, url).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonArray = JsonSerializer.Deserialize<AnalyzerListResponse>(content);
            return jsonArray?.Value;
        }

        /// <summary>
        /// Retrieves a specific analyzer detail through analyzerid from the content understanding service.
        /// This method sends a GET request to the service endpoint to get the analyzer detail.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer.</param>
        /// <returns>A dictionary containing the JSON response from the service, which includes the target analyzer detail.</returns>
        public async Task<Dictionary<string, object>> GetAnalyzerDetailByIdAsync(string analyzerId)
        {
            var url = GetAnalyzerUrl(analyzerId);
            var request = await CreateRequestAsync(HttpMethod.Get, url).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            
            return JsonSerializer.Deserialize<Dictionary<string, object>>(content) ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Initiates the creation of an analyzer with the given ID and schema.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer.</param>
        /// <param name="analyzerTemplate">The schema definition for the analyzer. Defaults to None.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer schema JSON file. Defaults to "".</param>
        /// <param name="storageContainerSasUrl">The SAS URL for the training storage container. Defaults to "".</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the training storage container. Defaults to "".</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The response object from the HTTP request.</returns>
        /// <exception cref="ArgumentException">If neither `analyzerTemplate` nor `analyzerTemplatePath` is provided.</exception>
        public async Task<HttpResponseMessage> BeginCreateAnalyzerAsync(
            string analyzerId,
            string analyzerTemplatePath = "",
            string storageContainerSasUrl = "",
            string storageContainerPathPrefix = "",
            bool isProMode = false,
            CancellationToken cancellationToken = default)
        {
            JsonDocument? analyzerTemplate = null;

            if (!string.IsNullOrEmpty(analyzerTemplatePath) && File.Exists(analyzerTemplatePath))
            {
                var jsonString = await File.ReadAllTextAsync(analyzerTemplatePath, cancellationToken).ConfigureAwait(false);
                analyzerTemplate = JsonDocument.Parse(jsonString);
            }

            if (analyzerTemplate == null)
                throw new ArgumentException("Analyzer schema must be provided either through analyzerTemplate or analyzerTemplatePath");

            var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(
                analyzerTemplate.RootElement.GetRawText());

            if (jsonObject != null && !string.IsNullOrEmpty(storageContainerSasUrl) && !string.IsNullOrEmpty(storageContainerPathPrefix))
            {
                if (!storageContainerPathPrefix.EndsWith("/"))
                {
                    storageContainerPathPrefix += "/";
                }

                if (isProMode)
                {
                    var referenceDocsConfig = GetProModeReferenceDocsConfig(storageContainerSasUrl, storageContainerPathPrefix);
                    jsonObject["knowledgeSources"] = referenceDocsConfig;
                }
                else
                {
                    var trainingConfig = GetTrainingDataConfig(storageContainerSasUrl, storageContainerPathPrefix);
                    jsonObject["trainingData"] = trainingConfig;
                }
            }

            var url = GetAnalyzerUrl(analyzerId);
            var content = new StringContent(
                    JsonSerializer.Serialize(jsonObject),
                    Encoding.UTF8,
                    "application/json");
            var request = await CreateRequestAsync(HttpMethod.Put, url, content).ConfigureAwait(false);
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
            var request = await CreateRequestAsync(HttpMethod.Delete, url).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            return response;
        }

        /// <summary>
        /// Begins the analysis of a file or URL using the specified analyzer.
        /// </summary>
        /// <param name="analyzerId">The ID of the analyzer to use.</param>
        /// <param name="fileLocation">The path to the file or the URL to analyze.</param>
        /// <returns>The response from the analysis request.</returns>
        public async Task<HttpResponseMessage> BeginAnalyzeAsync(string analyzerId, string fileLocation)
        {
            if (string.IsNullOrEmpty(analyzerId)) throw new ArgumentNullException("Parameters 'analyzerId' can't be null or empty.");

            HttpContent content;
            
            if (Uri.IsWellFormedUriString(fileLocation, UriKind.Absolute))
            {
                var data = new Dictionary<string, string> { ["url"] = fileLocation };
                content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            }
            else if (Directory.Exists(fileLocation))
            {
                var data = new
                {
                    inputs = Directory.GetFiles(fileLocation, "*", SearchOption.AllDirectories)
                        .Where(t => File.Exists(t) && IsSupportedDocTypeByFilePath(t, isDocument: true))
                        .Select(s => new
                        {
                            name = string.Join("_", Path.GetRelativePath(fileLocation, s).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                            data = Convert.ToBase64String(File.ReadAllBytes(s))
                        })
                        .ToArray()
                };
                content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
            }
            else if (File.Exists(fileLocation))
            {
                var bytes = await File.ReadAllBytesAsync(fileLocation).ConfigureAwait(false);
                content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            }
            else
            {
                throw new ArgumentException("File location must be a valid path or URL.");
            }

            var url = GetAnalyzeUrl(analyzerId);
            var request = await CreateRequestAsync(HttpMethod.Post, url, content).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

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
        /// Uploads a file to a specified blob storage path asynchronously.
        /// </summary>
        /// <remarks>This method overwrites any existing blob at the target path.</remarks>
        /// <param name="blobContainer">The <see cref="BlobContainerClient"/> representing the target blob container.</param>
        /// <param name="filePath">The local file path of the file to be uploaded. Cannot be null or empty.</param>
        /// <param name="targetBlobPath">The path within the blob container where the file will be uploaded. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        public async Task UploadFileToBlobAsync(BlobContainerClient blobContainer, string filePath, string targetBlobPath)
        {
            try
            {
                var blobClient = blobContainer.GetBlobClient(targetBlobPath);
                await blobClient.UploadAsync(filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload file '{filePath}' to blob '{targetBlobPath}'.", ex);
            }
        }

        /// <summary>
        /// Uploads JSON content to a specified blob in an Azure Blob Storage container.
        /// </summary>
        /// <remarks>This method overwrites any existing blob at the specified path. Ensure that the
        /// <paramref name="jsonContent"/> is properly formatted JSON.</remarks>
        /// <param name="blobContainer">The <see cref="BlobContainerClient"/> representing the target blob container.</param>
        /// <param name="jsonContent">The JSON content to upload as a string. Cannot be null or empty.</param>
        /// <param name="targetBlobPath">The path within the blob container where the JSON content will be uploaded. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        public async Task UploadJsonToBlobAsync(BlobContainerClient blobContainer, string jsonContent, string targetBlobPath)
        {
            try
            {
                var blobClient = blobContainer.GetBlobClient(targetBlobPath);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
                await blobClient.UploadAsync(stream, overwrite: true);
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload JSON content to blob '{targetBlobPath}'.", ex);
            }
        }

        /// <summary>
        /// Uploads a list of JSONL (JSON Lines) formatted strings to a specified blob in Azure Blob Storage.
        /// </summary>
        /// <remarks>This method overwrites any existing blob at the specified path. Ensure that the
        /// <paramref name="targetBlobPath"/> is correct to avoid unintentional data loss.</remarks>
        /// <param name="blobContainer">The <see cref="BlobContainerClient"/> representing the Azure Blob Storage container where the data will be
        /// uploaded.</param>
        /// <param name="jsonContents">A list of strings, each representing a JSON object, to be uploaded as JSONL content.</param>
        /// <param name="targetBlobPath">The path within the blob container where the JSONL content will be stored.</param>
        /// <returns>A task that represents the asynchronous upload operation.</returns>
        public async Task UploadJsonlToBlobAsync(BlobContainerClient blobContainer, List<string> jsonContents, string targetBlobPath)
        {
            try
            {
                var blobClient = blobContainer.GetBlobClient(targetBlobPath);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join("\n", jsonContents)));
                await blobClient.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to upload JSONL content to blob '{targetBlobPath}'.", ex);
            }
        }

        /// Initiates the creation of an classifier with the given ID and schema.
        /// </summary>
        /// <param name="classifierId">The unique identifier for the classifier. Cannot be null or whitespace.</param>
        /// <param name="classifierSchema">The JSON schema defining the classifier. Cannot be null or whitespace.</param>
        /// <returns>A task representing the asynchronous operation, with a <see cref="HttpResponseMessage"/> indicating the
        /// result of the HTTP request.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="classifierId"/> or <paramref name="classifierSchema"/> is null or whitespace.</exception>
        public async Task<HttpResponseMessage> BeginCreateClassifierAsync(
            string classifierId,
            string classifierSchema)
        {
            if (string.IsNullOrWhiteSpace(classifierId))
            {
                throw new ArgumentException("Classifier id must be provided.");
            }

            if (string.IsNullOrWhiteSpace(classifierSchema))
            {
                throw new ArgumentException("Classifier schema must be provided.");
            }

            var url = GetClassifierUrl(classifierId);
            var content = new StringContent(
                classifierSchema,
                Encoding.UTF8,
                "application/json");

            var request = await CreateRequestAsync(HttpMethod.Put, url, content).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return response;
        }

        /// <summary>
        /// Begins the analysis of a file or URL using the specified classifier.
        /// </summary>
        /// <param name="classifierId">The unique identifier of the classifier to be used. Cannot be null or empty.</param>
        /// <param name="fileLocation">The location of the file to be classified. This can be a valid file path or a well-formed URL.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message from
        /// the classification request.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="classifierId"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="fileLocation"/> is neither a valid file path nor a well-formed URL.</exception>
        public async Task<HttpResponseMessage> BeginClassifierAsync(
            string classifierId,
            string fileLocation)
        {
            if (string.IsNullOrEmpty(classifierId))
            {
                throw new ArgumentNullException("Parameters 'classifierId' can't be null or empty.");
            }

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
                throw new ArgumentException("File location must be a valid path or URL. Please update fileLocation to point to your PDF file.");
            }

            var url = GetClassifyUrl(classifierId);
            var request = await CreateRequestAsync(HttpMethod.Post, url, content).ConfigureAwait(false);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return response;
        }

        /// <summary>
        /// Determines whether the specified file extension is supported for the given document type.
        /// </summary>
        /// <param name="fileExt">The file extension to check, without a leading period. This value is case-insensitive.</param>
        /// <param name="isDocument">A boolean indicating whether to check against the document file types.  <see langword="true"/> to check
        /// against document file types; otherwise, checks against text file types.</param>
        /// <returns><see langword="true"/> if the file extension is supported for the specified document type; otherwise, <see
        /// langword="false"/>.</returns>
        public bool IsSupportedDocTypeByFileExt(string fileExt, bool isDocument = false)
        {
            var supportedTypes = isDocument ? SUPPORTED_FILE_TYPES_DOCUMENT : SUPPORTED_FILE_TYPES_DOCUMENT_TXT;
            return supportedTypes.Contains(fileExt.ToLower());
        }

        /// <summary>
        /// Determines whether the specified file path corresponds to a supported document type.
        /// </summary>
        /// <param name="filePath">The path of the file to check. Must not be null or empty.</param>
        /// <param name="isDocument">Indicates whether the file is expected to be a document. Defaults to <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if the file exists and its extension is supported; otherwise, <see
        /// langword="false"/>.</returns>
        public bool IsSupportedDocTypeByFilePath(string filePath, bool isDocument = false)
        {
            if (!File.Exists(filePath))
                return false;
            string fileExt = Path.GetExtension(filePath).ToLower();
            return IsSupportedDocTypeByFileExt(fileExt, isDocument);
        }

        /// <summary>
        /// Generates a Shared Access Signature (SAS) URL for a specified Azure Blob Storage container.
        /// </summary>
        /// <remarks>The generated SAS URL grants access to the specified container with the specified
        /// permissions for the specified duration. The method uses the Azure Identity library to authenticate and
        /// generate the SAS token.</remarks>
        /// <param name="accountName">The name of the Azure Storage account. This value cannot be null, empty, or whitespace.</param>
        /// <param name="containerName">The name of the blob container for which the SAS URL is generated. This value cannot be null, empty, or
        /// whitespace.</param>
        /// <param name="permissions">The permissions to include in the SAS token. If not specified, the default permissions are  <see
        /// cref="BlobContainerSasPermissions.Read"/>, <see cref="BlobContainerSasPermissions.Write"/>, and <see
        /// cref="BlobContainerSasPermissions.List"/>.</param>
        /// <param name="expiryHours">The number of hours until the SAS token expires. The default value is 1 hour. Must be a positive integer.</param>
        /// <returns>A string containing the SAS URL for the specified container, including the SAS token.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="accountName"/> or <paramref name="containerName"/> is null, empty, or consists
        /// only of whitespace.</exception>
        public async Task<string> GenerateContainerSasUrlAsync(
            string accountName, 
            string containerName,
            BlobContainerSasPermissions? permissions = null,
            int expiryHours = 1)
        {
            Console.WriteLine($"accountName: {accountName}");

            if(string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Account name and container name must be provided.");
            }

            var sasPermissions = permissions ?? BlobContainerSasPermissions.Read | BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List;
            var now = DateTimeOffset.UtcNow;
            var expiry = now.AddHours(expiryHours);
            var accountUrl = $"https://{accountName}.blob.core.windows.net";
            var blobServiceClient = new BlobServiceClient(new Uri(accountUrl), new DefaultAzureCredential());
            var delegationKey = await blobServiceClient.GetUserDelegationKeyAsync(now, expiry);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                Resource = "c", // c for container
                StartsOn = now,
                ExpiresOn = expiry,
                Protocol = SasProtocol.Https
            };
            
            sasBuilder.SetPermissions(sasPermissions);
            var sasToken = sasBuilder.ToSasQueryParameters(delegationKey, accountName).ToString();

            return $"{accountUrl}/{containerName}?{sasToken}";
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
