using System.Text.RegularExpressions;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Helper class for managing test recordings.
    /// Provides utilities for sanitizing sensitive data in recordings.
    /// </summary>
    public class TestRecordingHelper
    {
        /// <summary>
        /// Sanitizes sensitive data in test recordings.
        /// Replaces actual values with sanitized placeholders.
        /// </summary>
        /// <param name="recording">The test recording to sanitize.</param>
        public static void SanitizeRecording(TestRecording recording)
        {
            // Sanitize request headers
            recording.SanitizeHeader("Authorization");
            recording.SanitizeHeader("X-API-Key");
            recording.SanitizeHeader("x-ms-client-request-id");
            
            // Sanitize response headers
            recording.SanitizeResponseHeader("request-id");
            recording.SanitizeResponseHeader("x-ms-request-id");
            recording.SanitizeResponseHeader("apim-request-id");
            recording.SanitizeResponseHeader("x-ms-correlation-request-id");
            
            // Sanitize SAS token query parameters
            recording.SanitizeQueryParameter("sig", "Sanitized");
            recording.SanitizeQueryParameter("se", "2099-12-31T23:59:59Z");
            recording.SanitizeQueryParameter("sp", "racwdl");
            recording.SanitizeQueryParameter("sv", "2025-11-05");
            recording.SanitizeQueryParameter("sr", "c");
            recording.SanitizeQueryParameter("st", "2099-01-01T00:00:00Z");
            recording.SanitizeQueryParameter("skoid", "Sanitized");
            recording.SanitizeQueryParameter("sktid", "Sanitized");
            recording.SanitizeQueryParameter("skt", "2099-01-01T00:00:00Z");
            recording.SanitizeQueryParameter("ske", "2099-12-31T23:59:59Z");
            recording.SanitizeQueryParameter("sks", "b");
            recording.SanitizeQueryParameter("skv", "2025-11-05");
            
            // Sanitize endpoint URLs (replace real endpoints with sanitized)
            recording.SanitizeEndpoint("sanitized.services.ai.azure.com");
            recording.SanitizeStorageAccount("sanitizedaccount");
            
            // Sanitize request bodies (for SAS URLs in JSON payloads)
            recording.SanitizeBodyRegex(@"https://[^/]+\.blob\.core\.windows\.net/[^\s\""]+\?[^\s\""]+", GetSanitizedStorageSasUrl());
        }

        /// <summary>
        /// Gets the sanitized SAS URL for recordings.
        /// Replaces the actual SAS token with a placeholder.
        /// </summary>
        /// <param name="originalSasUrl">The original SAS URL with token.</param>
        /// <returns>A sanitized URL for use in recordings.</returns>
        public static string GetSanitizedSasUrl(string originalSasUrl)
        {
            // Extract base URL without query parameters
            var uri = new Uri(originalSasUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
            
            // Replace storage account name with sanitized version
            baseUrl = Regex.Replace(baseUrl, @"https://[^.]+\.blob\.core\.windows\.net", "https://sanitizedaccount.blob.core.windows.net");
            
            // Return base URL with sanitized token placeholder
            return $"{baseUrl}?skoid=Sanitized&sktid=Sanitized&skt=2099-01-01T00:00:00Z&ske=2099-12-31T23:59:59Z&sks=b&skv=2025-11-05&sv=2025-11-05&st=2099-01-01T00:00:00Z&se=2099-12-31T23:59:59Z&sr=c&sp=racwdl&sig=Sanitized";
        }
        
        /// <summary>
        /// Gets a standardized sanitized storage SAS URL for recordings.
        /// </summary>
        private static string GetSanitizedStorageSasUrl()
        {
            return "https://sanitizedaccount.blob.core.windows.net/sanitizedcontainer?skoid=Sanitized&sktid=Sanitized&skt=2099-01-01T00:00:00Z&ske=2099-12-31T23:59:59Z&sks=b&skv=2025-11-05&sv=2025-11-05&st=2099-01-01T00:00:00Z&se=2099-12-31T23:59:59Z&sr=c&sp=racwdl&sig=Sanitized";
        }
    }
}
