namespace AnalyzerTraining.Interfaces
{
    public interface IAnalyzerTrainingService
    {
        /// <summary>
        /// Create analyzer with defined schema.
        /// <remarks>Before creating the custom fields analyzer, you should fill the constant ANALYZER_ID with a business-related name. Here we randomly generate a name for demo purpose.
        /// We use **TRAINING_DATA_SAS_URL** and **TRAINING_DATA_PATH** that's set in the prerequisite step.</remarks>
        /// </summary>
        /// configuration.</param>
        /// <param name="trainingStorageContainerSasUrl">An optional SAS URL for the storage container containing the training data. If not provided, the method will use
        /// a default value.</param>
        /// <param name="trainingStorageContainerPathPrefix">An optional path prefix within the storage container to locate the training data. If not provided, the method
        /// will use a default value.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the unique identifier
        /// of the created analyzer.</returns>
        Task<string> CreateAnalyzerAsync(string analyzerTemplatePath, string trainingStorageContainerSasUrl, string trainingStorageContainerPathPrefix);

        /// <summary>
        /// ## Use created analyzer to extract document content.
        /// </summary>
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// <param name="analyzerId">The unique identifier of the custom analyzer to use for document analysis. This value must not be null or empty.</param>
        /// <param name="filePath">The file path of the document to analyze. The file must exist and be accessible.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when the document analysis is finished.</returns>
        Task AnalyzeDocumentWithCustomAnalyzerAsync(string analyzerId, string filePath);

        /// <summary>
        /// Delete exist analyzer in Content Understanding Service.
        /// </summary>
        /// <remarks>This snippet is not required, but it's only used to prevent the testing analyzer from residing in your service. The custom fields analyzer could be stored in your service for reusing by subsequent business in real usage scenarios.</remarks>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. This parameter cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
