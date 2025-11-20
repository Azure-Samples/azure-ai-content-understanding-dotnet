using System.Text.Json;

namespace Management.Interfaces
{
    public interface IManagementService
    {
        /// <summary>
        /// Create a simple analyzer.
        /// </summary>
        /// <remarks>We first create an analyzer from a template to extract invoice fields.</remarks>
        /// <returns>A <see cref="string"/> representing the unique identifier of the created analyzer.</returns>
        Task<JsonDocument> CreateAnalyzerAsync(string analyzerId, Dictionary<string, object> analyzerDefinition);

        /// <summary>
        /// List all analyzers created in your resource.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <returns></returns>
        Task<JsonElement[]?> ListAnalyzersAsync();

        /// <summary>
        /// Get analyzer details with id.
        /// </summary>
        /// <remarks>Remember the analyzer id when you create it. You can use the id to look up detail analyzer definitions afterwards.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer whose details are to be retrieved. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<string> GetAnalyzerDetailsAsync(string analyzerId);

        /// <summary>
        /// Delete Analyzer.
        /// </summary>
        /// <remarks>If you don't need an analyzer anymore, delete it with its id.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task DeleteAnalyzerAsync(string analyzerId);

        /// <summary>
        /// Update default model deployment mappings.
        /// </summary>
        /// <param name="modelDeployments">A dictionary mapping model names to deployment names.</param>
        /// <returns>A dictionary containing the updated default settings.</returns>
        Task<Dictionary<string, object>> UpdateDefaultsAsync(Dictionary<string, string?> modelDeployments);

        /// <summary>
        /// Get default settings including model deployment mappings.
        /// </summary>
        /// <returns>A dictionary containing the default settings.</returns>
        Task<Dictionary<string, object>> GetDefaultsAsync();
    }
}
