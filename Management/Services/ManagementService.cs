using ContentUnderstanding.Common;
using Management.Interfaces;
using System.Text.Json;

namespace Management.Services
{
    public class ManagementService : IManagementService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/management/";

        public ManagementService(AzureContentUnderstandingClient client)
        {
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Create a simple analyzer.
        /// </summary>
        /// <remarks>We first create an analyzer from a template to extract invoice fields.</remarks>
        /// <returns>A <see cref="string"/> representing the unique identifier of the created analyzer.</returns>
        public async Task<string> CreateAnalyzerAsync(string analyzerId, string analyzerTemplatePath)
        {
            Console.WriteLine("Creating Sample Analyzer...");

            Console.WriteLine($"Using template: {Path.GetFileName(analyzerTemplatePath)}");
            Console.WriteLine($"Analyzer ID: {analyzerId}");

            var createResponse = await _client.BeginCreateAnalyzerAsync(analyzerId, analyzerTemplatePath: analyzerTemplatePath);
            var resultJson = await _client.PollResultAsync(createResponse);
            var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

            Console.WriteLine("\nAnalyzer created successfully:");
            Console.WriteLine(serializedJson);

            return analyzerId;
        }

        /// <summary>
        /// List all analyzers created in your resource.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <returns></returns>
        public async Task<JsonElement[]?> ListAnalyzersAsync()
        {
            Console.WriteLine("\n===== Listing All Analyzers =====");

            JsonElement[]? analyzers = await _client.GetAllAnalyzersAsync();

            if(analyzers == null || analyzers.Length == 0)
            {
                Console.WriteLine("No analyzers found.");
                return null;
            }

            Console.WriteLine($"Number of analyzers: {analyzers.Length}");

            Console.WriteLine("\nFirst 3 analyzers:");
            for (int i = 0; i < Math.Min(3, analyzers.Length); i++)
            {
                Console.WriteLine($"- {analyzers[i].GetProperty("analyzerId")} ({analyzers[i].GetProperty("description")})");
            }

            if (analyzers.Length > 0)
            {
                Console.WriteLine("\nThe last analyzer details:");
                Console.WriteLine(JsonSerializer.Serialize(analyzers.Last(), new JsonSerializerOptions { WriteIndented = true }));
            }

            return analyzers;
        }

        /// <summary>
        /// Get analyzer details with id.
        /// </summary>
        /// <remarks>Remember the analyzer id when you create it. You can use the id to look up detail analyzer definitions afterwards.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer whose details are to be retrieved. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<Dictionary<string, object>> GetAnalyzerDetailsAsync(string analyzerId)
        {
            Console.WriteLine("\n===== Getting Analyzer Details =====");
            Console.WriteLine($"Analyzer ID: {analyzerId}");

            Dictionary<string, object> details = await _client.GetAnalyzerDetailByIdAsync(analyzerId);
            Console.WriteLine("\nAnalyzer Details:");
            Console.WriteLine(JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true }));

            return details;
        }

        /// <summary>
        /// Delete Analyzer.
        /// </summary>
        /// <remarks>If you don't need an analyzer anymore, delete it with its id.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        public async Task<HttpResponseMessage> DeleteAnalyzerAsync(string analyzerId)
        {
            Console.WriteLine("\n===== Deleting Analyzer =====");
            Console.WriteLine($"Analyzer ID: {analyzerId}");

            HttpResponseMessage response = await _client.DeleteAnalyzerAsync(analyzerId);
            Console.WriteLine("Analyzer deleted successfully");

            return response;
        }
    }
}
