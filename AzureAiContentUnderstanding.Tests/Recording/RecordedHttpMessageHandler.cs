using System.Net;
using System.Text;

namespace AzureAiContentUnderstanding.Tests.Recording
{
    /// <summary>
    /// HTTP message handler that records and plays back HTTP interactions.
    /// </summary>
    public class RecordedHttpMessageHandler : DelegatingHandler
    {
        private readonly RecordedTestMode mode;
        private readonly TestRecording recording;

        public RecordedHttpMessageHandler(RecordedTestMode mode, TestRecording recording)
            : base(new HttpClientHandler())
        {
            this.mode = mode;
            this.recording = recording;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            switch (mode)
            {
                case RecordedTestMode.Record:
                    return await RecordAsync(request, cancellationToken);

                case RecordedTestMode.Playback:
                    return await PlaybackAsync(request, cancellationToken);

                case RecordedTestMode.Live:
                default:
                    return await base.SendAsync(request, cancellationToken);
            }
        }

        private async Task<HttpResponseMessage> RecordAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Make the actual HTTP call
            var response = await base.SendAsync(request, cancellationToken);

            // Record the interaction
            var entry = await CreateRecordedEntryAsync(request, response);
            recording.AddEntry(entry);

            return response;
        }

        private async Task<HttpResponseMessage> PlaybackAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Find matching recorded entry
            var entry = recording.FindMatch(request);

            if (entry == null)
            {
                throw new InvalidOperationException(
                    $"No matching recording found for request: {request.Method} {request.RequestUri}\n" +
                    "Available recordings:\n" +
                    string.Join("\n", recording.GetAllEntries().Select(e => $"  {e.RequestMethod} {e.RequestUri}")));
            }

            // Create response from recorded data
            return CreateResponseFromRecording(entry, request);
        }

        private async Task<RecordedHttpEntry> CreateRecordedEntryAsync(
            HttpRequestMessage request,
            HttpResponseMessage response)
        {
            var entry = new RecordedHttpEntry
            {
                RequestUri = request.RequestUri!.ToString(),
                RequestMethod = request.Method.Method,
                RequestHeaders = new Dictionary<string, string>(),
                StatusCode = (int)response.StatusCode,
                ResponseHeaders = new Dictionary<string, string>()
            };

            // Record request headers (excluding sensitive ones) - Azure SDK format: single string value
            foreach (var header in request.Headers)
            {
                var headerValue = string.Join(", ", header.Value);
                if (!recording.SanitizedHeaders.Contains(header.Key))
                {
                    entry.RequestHeaders[header.Key] = headerValue;
                }
                else
                {
                    // Replace sanitized headers with placeholder
                    entry.RequestHeaders[header.Key] = "Sanitized";
                }
            }
            
            // Add Content-Type to request headers if present
            if (request.Content?.Headers.ContentType != null)
            {
                entry.RequestHeaders["Content-Type"] = request.Content.Headers.ContentType.ToString();
            }

            // Record request body
            if (request.Content != null)
            {
                var requestBody = await request.Content.ReadAsByteArrayAsync();
                var bodyString = System.Text.Encoding.UTF8.GetString(requestBody);
                
                // Apply body sanitization
                bodyString = SanitizeBody(bodyString);
                
                entry.RequestBody = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(bodyString));
            }

            // Record response headers (sanitize sensitive ones) - Azure SDK format: single string value
            foreach (var header in response.Headers)
            {
                var headerValue = string.Join(", ", header.Value);
                if (!recording.SanitizedResponseHeaders.Contains(header.Key))
                {
                    // Sanitize URLs in header values (like Operation-Location)
                    entry.ResponseHeaders[header.Key] = SanitizeUri(headerValue);
                }
                else
                {
                    entry.ResponseHeaders[header.Key] = "Sanitized";
                }
            }
            foreach (var header in response.Content.Headers)
            {
                if (!IsContentHeader(header.Key))
                {
                    var headerValue = string.Join(", ", header.Value);
                    entry.ResponseHeaders[header.Key] = headerValue;
                }
            }
            
            // Add Content-Type to response headers if present
            if (response.Content.Headers.ContentType != null)
            {
                entry.ResponseHeaders["Content-Type"] = response.Content.Headers.ContentType.ToString();
            }

            // Record response body with sanitization
            var responseBody = await response.Content.ReadAsByteArrayAsync();
            var responseBodyString = System.Text.Encoding.UTF8.GetString(responseBody);
            
            // Apply body sanitization to response
            responseBodyString = SanitizeBody(responseBodyString);
            
            entry.ResponseBody = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(responseBodyString));

            // Apply sanitization to request URIs (query params, endpoint, storage account)
            entry.RequestUri = SanitizeUri(entry.RequestUri);

            return entry;
        }

        private HttpResponseMessage CreateResponseFromRecording(
            RecordedHttpEntry entry,
            HttpRequestMessage originalRequest)
        {
            var response = new HttpResponseMessage((HttpStatusCode)entry.StatusCode);
            response.RequestMessage = originalRequest;

            // Restore response body
            if (!string.IsNullOrEmpty(entry.ResponseBody))
            {
                var bodyBytes = Convert.FromBase64String(entry.ResponseBody);
                response.Content = new ByteArrayContent(bodyBytes);
            }
            else
            {
                response.Content = new ByteArrayContent(Array.Empty<byte>());
            }

            // Restore response headers
            foreach (var header in entry.ResponseHeaders)
            {
                // Skip headers that belong to content
                if (IsContentHeader(header.Key))
                {
                    response.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                else
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return response;
        }

        private bool IsContentHeader(string headerName)
        {
            return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Content-Language", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Content-Location", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Content-MD5", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Content-Range", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Expires", StringComparison.OrdinalIgnoreCase) ||
                   headerName.Equals("Last-Modified", StringComparison.OrdinalIgnoreCase);
        }

        private string SanitizeUri(string uri)
        {
            var sanitizedUri = uri;
            
            // Sanitize endpoint (replace real endpoint with sanitized)
            if (!string.IsNullOrEmpty(recording.EndpointReplacement))
            {
                sanitizedUri = System.Text.RegularExpressions.Regex.Replace(
                    sanitizedUri,
                    @"https://[^/]+\.services\.ai\.azure\.com",
                    $"https://{recording.EndpointReplacement}",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            // Sanitize storage account (replace real storage account with sanitized)
            if (!string.IsNullOrEmpty(recording.StorageAccountReplacement))
            {
                sanitizedUri = System.Text.RegularExpressions.Regex.Replace(
                    sanitizedUri,
                    @"https://[^.]+\.blob\.core\.windows\.net",
                    $"https://{recording.StorageAccountReplacement}.blob.core.windows.net",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            // Sanitize query parameters
            foreach (var kvp in recording.SanitizedQueryParams)
            {
                // Replace query parameter values with sanitized versions
                var pattern = $@"([?&]){kvp.Key}=[^&]*";
                var replacement = $"$1{kvp.Key}={kvp.Value}";
                sanitizedUri = System.Text.RegularExpressions.Regex.Replace(
                    sanitizedUri,
                    pattern,
                    replacement,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return sanitizedUri;
        }

        private string SanitizeBody(string body)
        {
            var sanitizedBody = body;
            
            // Apply regex replacements to body
            foreach (var kvp in recording.BodyRegexReplacements)
            {
                sanitizedBody = System.Text.RegularExpressions.Regex.Replace(
                    sanitizedBody,
                    kvp.Key,
                    kvp.Value,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            
            return sanitizedBody;
        }
    }
}
