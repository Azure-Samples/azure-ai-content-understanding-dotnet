namespace AzureAiContentUnderstanding.Tests.Recording
{
    /// <summary>
    /// Represents a single recorded HTTP request/response pair in Azure SDK format.
    /// Flattened structure matching Azure.Core.TestFramework.
    /// </summary>
    public class RecordedHttpEntry
    {
        /// <summary>
        /// Gets or sets the request URI.
        /// </summary>
        public string RequestUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP method (GET, POST, PUT, DELETE, etc.).
        /// </summary>
        public string RequestMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request headers as key-value pairs.
        /// Azure SDK format: single string value per header (not array).
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the request body as a base64-encoded string, or null if no body.
        /// </summary>
        public string? RequestBody { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the response headers as key-value pairs.
        /// Azure SDK format: single string value per header (not array).
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the response body as a base64-encoded string, or null if no body.
        /// </summary>
        public string? ResponseBody { get; set; }
    }

    /// <summary>
    /// Represents the complete recording session.
    /// Azure SDK format: only contains Entries array, no Metadata.
    /// </summary>
    public class RecordingSession
    {
        /// <summary>
        /// Gets or sets the list of recorded HTTP entries.
        /// </summary>
        public List<RecordedHttpEntry> Entries { get; set; } = new();

        /// <summary>
        /// Gets or sets additional variables recorded during the session (optional).
        /// </summary>
        public Dictionary<string, string>? Variables { get; set; }
    }
}
