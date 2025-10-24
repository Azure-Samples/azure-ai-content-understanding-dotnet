using Azure.AI.ContentUnderstanding;
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
        Task<string> CreateAnalyzerAsync(string analyzerId, ContentAnalyzer analyzer);

        /// <summary>
        /// List all analyzers created in your resource.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <returns></returns>
        Task<List<ContentAnalyzer>> ListAnalyzersAsync();

        /// <summary>
        /// Get analyzer details with id.
        /// </summary>
        /// <remarks>Remember the analyzer id when you create it. You can use the id to look up detail analyzer definitions afterwards.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer whose details are to be retrieved. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<ContentAnalyzer> GetAnalyzerDetailsAsync(string analyzerId);

        /// <summary>
        /// Update an existing analyzer's description and tags.
        /// </summary>
        /// <remarks>This method updates only the mutable properties of an analyzer (description and tags). 
        /// To remove a tag, set its value to an empty string. To update a tag, set it to a new value. 
        /// To add a tag, add a new key-value pair.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to update. Cannot be null or empty.</param>
        /// <returns>A <see cref="ContentAnalyzer"/> representing the updated analyzer with the new description and tags.</returns>
        Task<ContentAnalyzer> UpdateAnalyzerAsync(string analyzerId);

        /// <summary>
        /// Delete Analyzer.
        /// </summary>
        /// <remarks>If you don't need an analyzer anymore, delete it with its id.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
