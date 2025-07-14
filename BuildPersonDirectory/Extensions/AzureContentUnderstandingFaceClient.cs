using BuildPersonDirectory.Models;
using ContentUnderstanding.Common;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BuildPersonDirectory.Extensions
{
    /// <summary>
    /// AzureContentUnderstandingFaceClient
    /// </summary>
    public class AzureContentUnderstandingFaceClient
    {
        private readonly HttpClient _httpClient;
        private readonly IOptions<ContentUnderstandingOptions> _options;
        private readonly Func<Task<string>> _tokenProvider;

        public AzureContentUnderstandingFaceClient(HttpClient httpClient, IOptions<ContentUnderstandingOptions> options, Func<Task<string>> tokenProvider)
        {
            _httpClient = httpClient;
            _options = options;
            _tokenProvider = tokenProvider;
        }

        /// <summary>
        /// Creates a new person directory with the specified identifier.
        /// </summary>
        /// <remarks>This method sends an HTTP PUT request to create the person directory.  Ensure that <paramref
        /// name="personDirectoryId"/> is a valid identifier  and that the server is configured to accept the
        /// request.</remarks>
        /// <param name="personDirectoryId">The unique identifier for the person directory to be created.  This value cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task CreatePersonDirectoryAsync(string personDirectoryId, string? description = null, Dictionary<string, dynamic>? tags = null)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["description"] = description,
                ["tags"] = tags
            };

            var request = await CreateRequestAsync(HttpMethod.Put, $"personDirectories/{personDirectoryId}", requestBody);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Updates the specified person directory with a new description and tags.
        /// </summary>
        /// <param name="personDirectoryId">The unique identifier of the person directory to update. Cannot be null or empty.</param>
        /// <param name="description">The new description for the person directory. This value replaces the existing description.</param>
        /// <param name="tags">A dictionary of key-value pairs representing the tags to associate with the person directory. Existing tags will
        /// be replaced with the provided tags. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task UpdatePersonDirectoryAsync(
            string personDirectoryId,
            string description,
            Dictionary<string, dynamic>? tags)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["description"] = description,
                ["tags"] = tags
            };

            var request = await CreateRequestAsync(
                HttpMethod.Patch,
                $"personDirectories/{personDirectoryId}",
                requestBody);

            await SendRequestAsync(request);
        }

        /// <summary>
        /// Adds a new person to the specified person directory.
        /// </summary>
        /// <remarks>This method sends an asynchronous HTTP POST request to create a new person in the specified
        /// directory. Ensure that the <paramref name="personDirectoryId"/> corresponds to a valid directory and that the
        /// <paramref name="tags"/> dictionary contains valid metadata.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory where the person will be added. This value cannot be null or
        /// empty.</param>
        /// <param name="tags">A dictionary of key-value pairs representing metadata or attributes associated with the person. Keys and values
        /// cannot be null. The dictionary may be empty if no tags are required.</param>
        /// <returns>A <see cref="PersonResponse"/> object containing details about the newly added person.</returns>
        public async Task<PersonResponse> AddPersonAsync(
            string personDirectoryId,
            Dictionary<string, dynamic> tags)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["tags"] = tags
            };
            var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"personDirectories/{personDirectoryId}/persons",
                requestBody);
            return await SendRequestAsync<PersonResponse>(request);
        }

        /// <summary>
        /// Updates the specified person's information in the given person directory.
        /// </summary>
        /// <remarks>This method allows updating tags and associated face IDs for a person. If <paramref
        /// name="tags"/> or <paramref name="faceIds"/> is null, the corresponding data will not be updated.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory containing the person to update. Cannot be null or empty.</param>
        /// <param name="personId">The unique identifier of the person to update. Cannot be null or empty.</param>
        /// <param name="tags">A dictionary of tags to associate with the person. Keys and values must be non-null strings. If null, tags will
        /// remain unchanged.</param>
        /// <param name="faceIds">A list of face IDs to associate with the person. If null, face IDs will remain unchanged.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task UpdatePersonAsync(
            string personDirectoryId,
            string personId,
            Dictionary<string, dynamic>? tags = null,
            List<string>? faceIds = null)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["tags"] = tags,
                ["faceIds"] = faceIds
            };
            var request = await CreateRequestAsync(
                HttpMethod.Patch,
                $"personDirectories/{personDirectoryId}/persons/{personId}",
                requestBody);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Adds a face to a specified person directory using the provided image data.
        /// </summary>
        /// <remarks>This method sends an asynchronous HTTP POST request to add a face to the specified person
        /// directory. Ensure that the <paramref name="imageData"/> is properly formatted as a base64 string and that the
        /// <paramref name="personDirectoryId"/> corresponds to an existing directory.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory to which the face will be added.</param>
        /// <param name="imageData">The base64-encoded image data representing the face to be added.</param>
        /// <param name="personId">The optional unique identifier of the person to associate with the face. If not provided, the face will be added
        /// without a specific person association.</param>
        /// <returns>A <see cref="FaceResponse"/> object containing details about the added face, including its unique identifier and
        /// associated metadata.</returns>
        public async Task<FaceResponse> AddFaceAsync(
            string personDirectoryId,
            string imageData,
            string? personId = null)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["faceSource"] = new { data = imageData },
                ["personId"] = personId
            };

            var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"personDirectories/{personDirectoryId}/faces",
                requestBody);
            return await SendRequestAsync<FaceResponse>(request);
        }

        /// <summary>
        /// Detects faces in the provided image data using the Azure Content Understanding service.
        /// </summary>
        /// <param name="data">The base64-encoded image data representing the image to be analyzed.</param>
        /// <returns>A task representing the asynchronous operation, containing a <see cref="FaceDetectionResponse"/> object with the results.</returns>
        public async Task<FaceDetectionResponse> DetectFacesAsync(string data)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["data"] = data            
            };

            var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"faces:detect",
                requestBody);
            return await SendRequestAsync<FaceDetectionResponse>(request);
        }

        /// <summary>
        /// Identifies a person in an image using the specified person directory and bounding box.
        /// </summary>
        /// <remarks>This method sends a request to the identification service, which uses the provided image and
        /// bounding box to identify a person within the specified person directory. Ensure that the bounding box accurately
        /// defines the region containing the face to improve identification accuracy.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory to search within. This parameter cannot be null or empty.</param>
        /// <param name="imageData">The base64-encoded image data containing the face to be identified. This parameter cannot be null or empty.</param>
        /// <param name="boundingBox">A dictionary representing the bounding box around the face in the image. The keys and values in the dictionary
        /// should define the coordinates and dimensions of the bounding box. This parameter cannot be null.</param>
        /// <returns>A <see cref="PersonIdentificationResponse"/> object containing the identification results, including the
        /// identified person's details and confidence scores. Returns null if the identification fails or no match is
        /// found.</returns>
        public async Task<PersonIdentificationResponse> IdentifyPersonAsync(
            string personDirectoryId,
            string imageData,
            Dictionary<string, dynamic> boundingBox)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["faceSource"] = new
                {
                    data = imageData,
                    targetBoundingBox = boundingBox
                }
            };

            var request = await CreateRequestAsync(
                HttpMethod.Post,
                $"personDirectories/{personDirectoryId}/persons:identify",
                requestBody);
            return await SendRequestAsync<PersonIdentificationResponse>(request);
        }

        /// <summary>
        /// Retrieves information about a specific person from the specified person directory.
        /// </summary>
        /// <remarks>This method performs an asynchronous HTTP GET request to fetch the person's details.  Ensure
        /// that the provided identifiers are valid and correspond to existing resources.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory containing the person.  This value cannot be null or empty.</param>
        /// <param name="personId">The unique identifier of the person to retrieve.  This value cannot be null or empty.</param>
        /// <returns>A <see cref="PersonResponse"/> object containing the details of the requested person.</returns>
        public async Task<PersonResponse> GetPersonAsync(string personDirectoryId, string personId)
        {
            var request = await CreateRequestAsync(HttpMethod.Get, $"personDirectories/{personDirectoryId}/persons/{personId}");
            return await SendRequestAsync<PersonResponse>(request);
        }

        public async Task UpdateFaceAsync(
            string personDirectoryId,
            string faceId,
            string personId)
        {
            var requestBody = new Dictionary<string, dynamic>
            {
                ["personId"] = personId
            };
            var request = await CreateRequestAsync(
                HttpMethod.Patch,
                $"personDirectories/{personDirectoryId}/faces/{faceId}",
                requestBody).ConfigureAwait(false);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Deletes a specific face from a person directory.
        /// </summary>
        /// <remarks>This method sends a DELETE request to remove the specified face from the given person
        /// directory. Ensure that both <paramref name="personDirectoryId"/> and <paramref name="faceId"/> are valid and
        /// exist.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory containing the face to be deleted.</param>
        /// <param name="faceId">The unique identifier of the face to be deleted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DeleteFaceAsync(string personDirectoryId, string faceId)
        {
            var request = await CreateRequestAsync(
                HttpMethod.Delete,
                $"personDirectories/{personDirectoryId}/faces/{faceId}",
                null);
            await SendRequestAsync(request);
        }

        /// <summary>
        /// Deletes a person from the specified person directory.
        /// </summary>
        /// <remarks>This method sends a DELETE request to the server to remove the specified person. Ensure that
        /// the identifiers provided are valid and correspond to existing resources.</remarks>
        /// <param name="personDirectoryId">The unique identifier of the person directory containing the person to delete. This parameter cannot be null or
        /// empty.</param>
        /// <param name="personId">The unique identifier of the person to delete. This parameter cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task DeletePersonAsync(string personDirectoryId, string personId)
        {
            var request = await CreateRequestAsync(HttpMethod.Delete, $"personDirectories/{personDirectoryId}/persons/{personId}");
            await SendRequestAsync(request);
        }

        #region Helper Methods
        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, object? content = null)
        {
            var url = $"{_options.Value.Endpoint}/contentunderstanding/{path}?api-version={_options.Value.ApiVersion}";
            var request = new HttpRequestMessage(method, url);

            // Add authentication
            if (!string.IsNullOrEmpty(_options.Value.SubscriptionKey))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", _options.Value.SubscriptionKey);
            }
            else if (_tokenProvider != null)
            {
                var token = await _tokenProvider().ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Add user agent
            request.Headers.Add("x-ms-useragent", _options.Value.UserAgent);

            // Serialize content if provided
            if (content != null)
            {
                var json = JsonSerializer.Serialize(content);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private async Task SendRequestAsync(HttpRequestMessage request)
        {
            try
            {
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"API error {response.StatusCode}: {errorContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task<T> SendRequestAsync<T>(HttpRequestMessage request)
        {
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorDetail = JsonSerializer.Deserialize<dynamic>(content);
                throw new HttpRequestException($"Validation failed: {errorDetail?.Message}", errorDetail);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"API error {(int)response.StatusCode} {response.StatusCode}: {content}",
                    null,
                    response.StatusCode);
            }

            return JsonSerializer.Deserialize<T>(content);
        }

        public static string ReadFileToBase64(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            return Convert.ToBase64String(bytes);
        }
        #endregion
    }
}
