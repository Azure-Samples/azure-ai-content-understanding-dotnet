namespace ConversationalFieldExtraction.Interfaces
{
    public interface IConversationalFieldExtractionService
    {
        /// <summary>
        /// Create Analyzer from the Template.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must not be null or empty.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template. This value must point to a valid template file and must not be null or
        /// empty.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CreateAnalyzerFromTemplateAsync(string analyzerId, string analyzerTemplatePath);

        /// <summary>
        /// Extract Fields Using the Analyzer
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// </summary>
        /// <param name="analyzerId"></param>
        /// <param name="sampleFilePath"></param>
        /// <returns></returns>
        Task ExtractFieldsWithAnalyzerAsync(string analyzerId, string sampleFilePath);

        /// <summary>
        /// Clean Up
        /// <remarks>Optionally, delete the sample analyzer from your resource. In typical usage scenarios, you would analyze multiple files using the same analyzer.</remarks>
        /// </summary>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>

        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
