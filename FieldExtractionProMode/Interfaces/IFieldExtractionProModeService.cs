
using System.Text.Json;

namespace FieldExtractionProMode.Interfaces
{
    public interface IFieldExtractionProModeService
    {
        /// <summary>
        /// Generates a knowledge base by analyzing documents in a specified folder and uploading the results to a blob
        /// storage container.
        /// </summary>
        /// <remarks>This method analyzes documents using a prebuilt document analyzer and uploads both
        /// the original and result files to the specified blob storage. If <paramref name="skipAnalyze"/> is <see
        /// langword="true"/>, only the files are uploaded without analysis.</remarks>
        /// <param name="referenceDocsFolder">The path to the folder containing reference documents to be analyzed.</param>
        /// <param name="storageContainerSasUrl">The SAS URL of the blob storage container where results will be uploaded.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container for uploaded files. Must end with a slash.</param>
        /// <param name="skipAnalyze">If <see langword="true"/>, skips the analysis step and only uploads files. Otherwise, performs analysis
        /// before uploading.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a document analysis fails or if the JSON result cannot be parsed. Ensure the documents are valid
        /// and the analyzer is correctly configured.</exception>
        Task GenerateKnowledgeBaseOnBlobAsync(
            string referenceDocsFolder,
            string storageContainerSasUrl,
            string storageContainerPathPrefix,
            bool skipAnalyze = false);

        /// <summary>
        /// Asynchronously creates an analyzer with a specified schema in Pro Mode.
        /// </summary>
        /// <remarks>This method initiates the creation of an analyzer in Pro Mode using the specified
        /// schema and reference documents. It logs the outcome of the operation and throws an exception if an error
        /// occurs during the creation process.</remarks>
        /// <param name="analyzerId">The unique identifier for the analyzer. Must not be null, empty, or whitespace.</param>
        /// <param name="analyzerSchema">The schema definition for the analyzer. Must not be null, empty, or whitespace.</param>
        /// <param name="proModeReferenceDocsStorageContainerSasUrl">The SAS URL for the storage container containing reference documents for Pro Mode.</param>
        /// <param name="proModeReferenceDocsStorageContainerPathPrefix">The path prefix within the storage container for reference documents in Pro Mode.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="analyzerId"/> or <paramref name="analyzerSchema"/> is null, empty, or consists
        /// only of white-space characters.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the analyzer creation fails due to an error in the provided configuration or deployment.</exception>
        Task<JsonDocument> CreateAnalyzerWithDefinedSchemaForProModeAsync(
            string analyzerId,
            string analyzerSchema,
            string proModeReferenceDocsStorageContainerSasUrl,
            string proModeReferenceDocsStorageContainerPathPrefix
            );

        /// <summary>
        /// Analyzes a document using a predefined schema in professional mode.
        /// </summary>
        /// <remarks>This method processes the document asynchronously and applies the specified analyzer
        /// schema. Ensure that the  <paramref name="analyzerId"/> corresponds to a valid and available analyzer
        /// configuration.</remarks>
        /// <param name="analyzerId">The identifier of the analyzer to be used for processing the document.</param>
        /// <param name="fileLocation">The file path of the document to be analyzed. Must be a valid path to an existing file.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task<JsonDocument> AnalyzeDocumentWithDefinedSchemaForProModeAsync(string analyzerId, string fileLocation);

        /// <summary>
        /// Delete exist analyzer in Content Understanding Service.
        /// </summary>
        /// <remarks>This snippet is not required, but it's only used to prevent the testing analyzer from residing in your service. The custom fields analyzer could be stored in your service for reusing by subsequent business in real usage scenarios.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. This parameter cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
