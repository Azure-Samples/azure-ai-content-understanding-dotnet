using System.Runtime.CompilerServices;
using System.Text.Json;
using AzureAiContentUnderstanding.Tests.Recording;

namespace AzureAiContentUnderstanding.Tests
{
    /// <summary>
    /// Base class for tests that support HTTP recording and playback.
    /// Provides recording infrastructure for capturing and replaying HTTP interactions.
    /// </summary>
    public abstract class RecordedTestBase : IDisposable
    {
        private readonly string sessionRecordPath;
        private readonly string recordingFilePath;

        /// <summary>
        /// Gets the current test recording mode.
        /// </summary>
        protected RecordedTestMode Mode { get; }

        /// <summary>
        /// Gets the recording for the current test session.
        /// </summary>
        protected TestRecording Recording { get; }

        protected RecordedTestBase()
        {
            // Determine test mode from environment variable
            var modeStr = Environment.GetEnvironmentVariable("AZURE_TEST_MODE") ?? "Live";
            Mode = Enum.Parse<RecordedTestMode>(modeStr, ignoreCase: true);

            // Get the source directory (where the test project is located, not the build output)
            var sourceDirectory = GetSourceDirectory();
            
            // Create recording file path based on test class name
            var testClassName = GetType().Name;
            sessionRecordPath = Path.Combine(sourceDirectory, "SessionRecords");
            recordingFilePath = Path.Combine(sessionRecordPath, $"{testClassName}.json");
            
            Directory.CreateDirectory(sessionRecordPath);

            Recording = new TestRecording(Mode, sessionRecordPath);

            // Load existing recording in Playback mode
            if (Mode == RecordedTestMode.Playback)
            {
                LoadRecording();
            }
        }

        private void LoadRecording()
        {
            if (!File.Exists(recordingFilePath))
            {
                throw new InvalidOperationException(
                    $"Recording file not found: {recordingFilePath}\n" +
                    $"Please run the test in Record mode first to create the recording.");
            }

            var json = File.ReadAllText(recordingFilePath);
            var session = JsonSerializer.Deserialize<RecordingSession>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (session != null)
            {
                Recording.LoadSession(session);
                Console.WriteLine($"Loaded recording with {session.Entries.Count} entries from: {recordingFilePath}");
            }
        }

        private void SaveRecording()
        {
            var session = Recording.GetSession();
            
            // Azure SDK format: no Metadata, just Entries
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            
            File.WriteAllText(recordingFilePath, json);
            Console.WriteLine($"Recording saved with {session.Entries.Count} entries to: {recordingFilePath}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && Mode == RecordedTestMode.Record)
            {
                SaveRecording();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the source directory where the test project is located (not the build output directory).
        /// </summary>
        private static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "")
        {
            // Use the source file path to determine the project directory
            // This file is now in Recording subdirectory, so we need to go up one level
            if (!string.IsNullOrEmpty(sourceFilePath) && File.Exists(sourceFilePath))
            {
                var recordingDir = Path.GetDirectoryName(sourceFilePath);
                if (recordingDir != null)
                {
                    // Go up one directory from Recording to get the test project root
                    return Path.GetDirectoryName(recordingDir) ?? Directory.GetCurrentDirectory();
                }
            }

            // Fallback: Walk up from the current directory to find the project directory
            var currentDir = Directory.GetCurrentDirectory();
            var directory = new DirectoryInfo(currentDir);

            while (directory != null)
            {
                // Check if this directory contains a .csproj file
                var csprojFiles = directory.GetFiles("*.Tests.csproj");
                if (csprojFiles.Length > 0)
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            // Final fallback
            return currentDir;
        }
    }

    /// <summary>
    /// Test recording modes.
    /// </summary>
    public enum RecordedTestMode
    {
        /// <summary>
        /// Record HTTP interactions to disk.
        /// </summary>
        Record,

        /// <summary>
        /// Play back previously recorded HTTP interactions.
        /// </summary>
        Playback,

        /// <summary>
        /// Make live HTTP calls without recording.
        /// </summary>
        Live
    }

    /// <summary>
    /// Represents a test recording session.
    /// Manages HTTP request/response recording and playback.
    /// </summary>
    public class TestRecording
    {
        private readonly RecordedTestMode mode;
        private readonly string recordPath;
        private readonly HashSet<string> sanitizedHeaders = new();
        private readonly HashSet<string> sanitizedResponseHeaders = new();
        private readonly Dictionary<string, string> sanitizedQueryParams = new();
        private readonly Dictionary<string, string> bodyRegexReplacements = new();
        private string? endpointReplacement;
        private string? storageAccountReplacement;
        private RecordingSession session = new();
        private RecordMatcher? matcher;

        public TestRecording(RecordedTestMode mode, string recordPath)
        {
            this.mode = mode;
            this.recordPath = recordPath;
        }

        /// <summary>
        /// Adds a header to the list of headers that should be sanitized in recordings.
        /// </summary>
        public void SanitizeHeader(string headerName)
        {
            sanitizedHeaders.Add(headerName);
        }

        /// <summary>
        /// Adds a response header to the list of headers that should be sanitized in recordings.
        /// </summary>
        public void SanitizeResponseHeader(string headerName)
        {
            sanitizedResponseHeaders.Add(headerName);
        }

        /// <summary>
        /// Adds a query parameter to the list of parameters that should be sanitized in recordings.
        /// </summary>
        public void SanitizeQueryParameter(string paramName, string sanitizedValue = "Sanitized")
        {
            sanitizedQueryParams[paramName] = sanitizedValue;
        }

        /// <summary>
        /// Sets the endpoint to use for sanitization (replaces real endpoint with this value).
        /// </summary>
        public void SanitizeEndpoint(string sanitizedEndpoint)
        {
            endpointReplacement = sanitizedEndpoint;
        }

        /// <summary>
        /// Sets the storage account name to use for sanitization.
        /// </summary>
        public void SanitizeStorageAccount(string sanitizedAccount)
        {
            storageAccountReplacement = sanitizedAccount;
        }

        /// <summary>
        /// Adds a regex pattern to sanitize request/response bodies.
        /// </summary>
        public void SanitizeBodyRegex(string pattern, string replacement)
        {
            bodyRegexReplacements[pattern] = replacement;
        }

        /// <summary>
        /// Gets the sanitized headers collection.
        /// </summary>
        public IReadOnlySet<string> SanitizedHeaders => sanitizedHeaders;

        /// <summary>
        /// Gets the sanitized response headers collection.
        /// </summary>
        public IReadOnlySet<string> SanitizedResponseHeaders => sanitizedResponseHeaders;

        /// <summary>
        /// Gets the sanitized query parameters collection.
        /// </summary>
        public IReadOnlyDictionary<string, string> SanitizedQueryParams => sanitizedQueryParams;

        /// <summary>
        /// Gets the body regex replacements collection.
        /// </summary>
        public IReadOnlyDictionary<string, string> BodyRegexReplacements => bodyRegexReplacements;

        /// <summary>
        /// Gets the endpoint replacement value.
        /// </summary>
        public string? EndpointReplacement => endpointReplacement;

        /// <summary>
        /// Gets the storage account replacement value.
        /// </summary>
        public string? StorageAccountReplacement => storageAccountReplacement;

        /// <summary>
        /// Gets whether this recording is in playback mode.
        /// </summary>
        public bool IsPlayback => mode == RecordedTestMode.Playback;

        /// <summary>
        /// Adds a recorded HTTP entry to the session.
        /// </summary>
        public void AddEntry(RecordedHttpEntry entry)
        {
            session.Entries.Add(entry);
        }

        /// <summary>
        /// Finds a matching recorded entry for the given request.
        /// </summary>
        public RecordedHttpEntry? FindMatch(HttpRequestMessage request)
        {
            if (matcher == null)
            {
                matcher = new RecordMatcher(
                    session.Entries, 
                    sanitizedQueryParams.ToDictionary(k => k.Key, v => v.Value),
                    endpointReplacement,
                    storageAccountReplacement);
            }
            return matcher.FindMatch(request);
        }

        /// <summary>
        /// Gets all recorded entries.
        /// </summary>
        public IReadOnlyList<RecordedHttpEntry> GetAllEntries()
        {
            return session.Entries.AsReadOnly();
        }

        /// <summary>
        /// Loads a recording session from disk.
        /// </summary>
        public void LoadSession(RecordingSession loadedSession)
        {
            session = loadedSession;
            // Don't create matcher here - let it be lazy initialized in FindMatch()
            // This allows sanitizer configuration to be set first
            matcher = null;
        }

        /// <summary>
        /// Gets the current recording session.
        /// </summary>
        public RecordingSession GetSession()
        {
            return session;
        }

        /// <summary>
        /// Adds or updates a variable in the recording session.
        /// </summary>
        public void SetVariable(string key, string value)
        {
            session.Variables ??= new Dictionary<string, string>();
            session.Variables[key] = value;
        }

        /// <summary>
        /// Gets a variable from the recording session.
        /// </summary>
        public string? GetVariable(string key)
        {
            return session.Variables?.TryGetValue(key, out var value) == true ? value : null;
        }
    }
}
