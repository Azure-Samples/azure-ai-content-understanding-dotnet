using System.Text;
using System.Text.RegularExpressions;

namespace AzureAiContentUnderstanding.Tests.Recording
{
    /// <summary>
    /// Matches recorded HTTP requests to find corresponding responses during playback.
    /// </summary>
    public class RecordMatcher
    {
        private readonly List<RecordedHttpEntry> entries;
        private readonly HashSet<int> usedEntries = new();
        private readonly Dictionary<string, string> sanitizedQueryParams;
        private readonly string? endpointReplacement;
        private readonly string? storageAccountReplacement;

        public RecordMatcher(
            List<RecordedHttpEntry> entries, 
            Dictionary<string, string> sanitizedQueryParams,
            string? endpointReplacement = null,
            string? storageAccountReplacement = null)
        {
            this.entries = entries;
            this.sanitizedQueryParams = sanitizedQueryParams;
            this.endpointReplacement = endpointReplacement;
            this.storageAccountReplacement = storageAccountReplacement;
        }

        /// <summary>
        /// Finds a matching recorded entry for the given request.
        /// </summary>
        public RecordedHttpEntry? FindMatch(HttpRequestMessage request)
        {
            var requestUri = SanitizeUri(request.RequestUri!.ToString());
            var method = request.Method.Method;

            for (int i = 0; i < entries.Count; i++)
            {
                // Skip already used entries to support sequential matching
                if (usedEntries.Contains(i))
                    continue;

                var entry = entries[i];
                
                // Match method
                if (!string.Equals(entry.RequestMethod, method, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Match URI (with sanitization applied)
                var recordedUri = SanitizeUri(entry.RequestUri);
                if (!UrisMatch(requestUri, recordedUri))
                    continue;

                // Found a match
                usedEntries.Add(i);
                return entry;
            }

            return null;
        }

        /// <summary>
        /// Sanitizes a URI by replacing endpoint, storage account, and query parameter values.
        /// </summary>
        private string SanitizeUri(string uri)
        {
            var sanitizedUri = uri;
            
            // Sanitize endpoint (replace real endpoint with sanitized)
            if (!string.IsNullOrEmpty(endpointReplacement))
            {
                sanitizedUri = Regex.Replace(
                    sanitizedUri,
                    @"https://[^/]+\.services\.ai\.azure\.com",
                    $"https://{endpointReplacement}",
                    RegexOptions.IgnoreCase);
            }
            
            // Sanitize storage account (replace real storage account with sanitized)
            if (!string.IsNullOrEmpty(storageAccountReplacement))
            {
                sanitizedUri = Regex.Replace(
                    sanitizedUri,
                    @"https://[^.]+\.blob\.core\.windows\.net",
                    $"https://{storageAccountReplacement}.blob.core.windows.net",
                    RegexOptions.IgnoreCase);
            }
            
            // Sanitize query parameters
            if (sanitizedUri.Contains('?'))
            {
                var parts = sanitizedUri.Split('?');
                var baseUri = parts[0];
                var queryString = parts[1];

                foreach (var kvp in sanitizedQueryParams)
                {
                    var pattern = $@"{Regex.Escape(kvp.Key)}=[^&]*";
                    var replacement = $"{kvp.Key}={kvp.Value}";
                    queryString = Regex.Replace(queryString, pattern, replacement, RegexOptions.IgnoreCase);
                }

                sanitizedUri = $"{baseUri}?{queryString}";
            }
            
            return sanitizedUri;
        }

        /// <summary>
        /// Checks if two URIs match, considering sanitization.
        /// </summary>
        private bool UrisMatch(string uri1, string uri2)
        {
            // Normalize URIs for comparison
            uri1 = NormalizeUri(uri1);
            uri2 = NormalizeUri(uri2);

            return string.Equals(uri1, uri2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Normalizes a URI for comparison by sorting query parameters.
        /// </summary>
        private string NormalizeUri(string uri)
        {
            if (!uri.Contains('?'))
                return uri;

            var parts = uri.Split('?');
            var baseUri = parts[0];
            var queryParams = parts[1].Split('&')
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return $"{baseUri}?{string.Join("&", queryParams)}";
        }

        /// <summary>
        /// Resets the matcher to allow reusing recordings.
        /// </summary>
        public void Reset()
        {
            usedEntries.Clear();
        }
    }
}
