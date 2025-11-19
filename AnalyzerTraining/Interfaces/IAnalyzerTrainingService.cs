using System.Text.Json;

namespace AnalyzerTraining.Interfaces
{
    public interface IAnalyzerTrainingService
    {
        /// <summary>
        /// Uploads training data files, including labels and OCR results, from a local folder to a specified Azure Blob
        /// Storage container.
        /// </summary>
        /// <remarks>This method uploads training documents along with their associated label and OCR
        /// result files to Azure Blob Storage.  The method ensures that each document has corresponding label and OCR
        /// result files before uploading.  If any required file is missing, a <see cref="FileNotFoundException"/> is
        /// thrown.</remarks>
        /// <param name="trainingDocsFolder">The local folder containing the training documents, label files, and OCR result files.  Each document must
        /// have corresponding label and OCR result files in the same folder.</param>
        /// <param name="storageContainerSasUrl">The SAS URL of the Azure Blob Storage container where the files will be uploaded.</param>
        /// <param name="storageContainerPathPrefix">The path prefix within the storage container where the files will be uploaded.  If the prefix does not end
        /// with a forward slash ('/'), one will be appended automatically.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException">Thrown if a required label file or OCR result file is missing for a document in the specified folder.</exception>
        Task GenerateTrainingDataOnBlobAsync(string trainingDocsFolder, string storageContainerSasUrl, string storageContainerPathPrefix);

        /// <summary>
        /// Create analyzer with defined schema and labeled training data.
        /// <remarks>Before creating the custom fields analyzer, you should fill the constant ANALYZER_ID with a business-related name. Here we randomly generate a name for demo purpose.
        /// We use **TRAINING_DATA_SAS_URL** and **TRAINING_DATA_PATH** that's set in the prerequisite step.</remarks>
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer.</param>
        /// <param name="analyzerDefinition">The analyzer definition as a dictionary.</param>
        /// <param name="trainingStorageContainerSasUrl">The SAS URL for the storage container containing the training data.</param>
        /// <param name="trainingStorageContainerPathPrefix">The path prefix within the storage container to locate the training data.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the analyzer details as JsonDocument.</returns>
        Task<JsonDocument> CreateAnalyzerAsync(string analyzerId, Dictionary<string, object> analyzerDefinition, string trainingStorageContainerSasUrl, string trainingStorageContainerPathPrefix);

        /// <summary>
        /// ## Use created analyzer to extract document content.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <param name="analyzerId">The unique identifier of the custom analyzer to use for document analysis. This value must not be null or empty.</param>
        /// <param name="filePath">The file path of the document to analyze. The file must exist and be accessible.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the document analysis is finished and returns the result as JsonDocument.</returns>
        Task<JsonDocument> AnalyzeDocumentWithCustomAnalyzerAsync(string analyzerId, string filePath);

        /// <summary>
        /// Delete exist analyzer in Content Understanding Service.
        /// </summary>
        /// <remarks>This snippet is not required, but it's only used to prevent the testing analyzer from residing in your service. The custom fields analyzer could be stored in your service for reusing by subsequent business in real usage scenarios.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. This parameter cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
