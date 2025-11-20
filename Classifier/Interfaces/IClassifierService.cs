using System.Collections.Generic;
using System.Text.Json;

namespace Classifier.Interfaces
{
    public interface IClassifierService
    {
        /// <summary>
        /// Asynchronously classifies a document using an analyzer with contentCategories.
        /// </summary>
        /// <remarks>This method creates an analyzer with contentCategories (which acts as a classifier), 
        /// classifies the specified document, and then deletes the analyzer. Ensure that the file at 
        /// <paramref name="fileLocation"/> exists before calling this method.</remarks>
        /// <param name="classifierId">The unique identifier for the classifier analyzer to be used.</param>
        /// <param name="analyzerDefinition">The analyzer definition dictionary with contentCategories.</param>
        /// <param name="fileLocation">The file path of the document to classify. Must be a valid path to an existing file.</param>
        /// <returns>A <see cref="JsonDocument"/> containing the classification results, or <see langword="null"/> if the file
        /// is not found or an error occurs.</returns>
        Task<JsonDocument?> ClassifyDocumentAsync(string classifierId, Dictionary<string, object> analyzerDefinition, string fileLocation);

        /// <summary>
        /// Creates a custom analyzer for loan applications.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created.</param>
        /// <returns>The analyzer ID.</returns>
        Task<string> CreateLoanAnalyzerAsync(string analyzerId);

        /// <summary>
        /// Deletes a custom analyzer.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be deleted.</param>
        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
