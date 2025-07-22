using System.Text.Json;

namespace Classifier.Interfaces
{
    public interface IClassifierService
    {
        /// <summary>
        /// Create a Basic Classifier.
        /// </summary>
        /// <remarks>Create a simple classifier that categorizes documents without additional analysis.</remarks>
        /// <param name="classifierId">The unique identifier for the classifier to be created.</param>
        /// <param name="classifierSchemaPath">The file path to the schema used for creating the classifier. Must be a valid path to a readable file.</param>
        /// <returns></returns>
        Task CreateClassifierAsync(string classifierId, string classifierSchemaPath);

        /// <summary>
        /// Initiates the classification of a document using a specified classifier.
        /// </summary>
        /// <remarks>Use the classifier to categorize your document.</remarks>
        /// <param name="classifierId">The identifier of the classifier to be used for document classification. Cannot be null or empty.</param>
        /// <param name="fileLocation">The file path of the document to be classified. Must be a valid path to an existing file.</param>
        /// <returns></returns>
        Task ClassifyDocumentAsync(string classifierId, string fileLocation);

        /// <summary>
        /// Asynchronously creates an enhanced classifier using a custom analyzer.
        /// </summary>
        /// <remarks>This method first creates a custom analyzer using the specified schema and then uses
        /// it to create an enhanced classifier. The enhanced classifier is configured to process different types of
        /// documents with specified analyzers.</remarks>
        /// <param name="analyzerId">The unique identifier for the custom analyzer to be created.</param>
        /// <param name="analyzerSchemaPath">The file path to the schema definition for the custom analyzer.</param>
        /// <param name="enhancedSchemaPath">The file path to the schema definition for the enhanced classifier.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the unique identifier of the
        /// created enhanced classifier.</returns>
        Task<string> CreateEnhancedClassifierWithCustomAnalyzerAsync(string analyzerId, string analyzerSchemaPath, string enhancedSchemaPath);

        /// <summary>
        /// Processes a document using an enhanced classifier asynchronously.
        /// </summary>
        /// <remarks>Process the document again using our enhanced classifier. Invoices and loan application documents will now have additional fields extracted.</remarks>
        /// <param name="enhancedClassifierId">The identifier of the enhanced classifier to be used for processing.</param>
        /// <param name="fileLocation">The file path of the document to be processed.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ProcessDocumentWithEnhancedClassifierAsync(string enhancedClassifierId, string fileLocation);
    }
}
