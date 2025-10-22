using Azure.AI.ContentUnderstanding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConversationalFieldExtraction.Interfaces
{
    public interface IConversationalFieldExtractionService
    {
        /// <summary>
        /// Creates a new analyzer from a specified template file and polls for the completion of the creation operation.
        /// This method initiates the analyzer creation process using the Azure Content Understanding service and waits
        /// for the operation to complete before returning. The analyzer can then be used for conversational field extraction.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must not be null or empty.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template. This value must point to a valid template file and must not be null or
        /// empty.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<ContentAnalyzer> CreateAnalyzerFromTemplateAsync(string analyzerId, ContentAnalyzer analyzer);

        /// <summary>
        /// Extracts structured fields from conversation data using a specified analyzer.
        /// This method converts the input JSON file to WebVTT format, validates the conversion,
        /// and then uses the Azure Content Understanding service to analyze the conversation
        /// and extract structured field data based on the analyzer's configuration.
        /// </summary>
        /// <param name="analyzerId">The unique identifier of the analyzer to use for field extraction. Must not be null or empty.</param>
        /// <param name="filePath">The file path to the JSON conversation data file to be analyzed. Must point to a valid JSON file and must not be null or empty.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains a <see cref="JsonDocument"/> 
        /// with the extracted field data if successful, or null if the WebVTT conversion fails or is invalid.
        /// </returns>
        /// <exception cref="Exception">Thrown when there is an error during the field extraction process, including issues with file conversion, analyzer communication, or result polling.</exception>
        /// <remarks>
        /// The method performs the following operations:
        /// 1. Converts the input JSON file to WebVTT format for conversation analysis
        /// 2. Validates that the conversion produced valid WebVTT content
        /// 3. Initiates analysis using the specified analyzer through the Azure Content Understanding service
        /// 4. Polls for the completion of the analysis operation
        /// 5. Returns the structured field extraction results as JSON
        /// </remarks>
        Task<AnalyzeResult?> ExtractFieldsWithAnalyzerAsync(string analyzerId, string filePath);

        /// <summary>
        /// Clean Up
        /// <remarks>Optionally, delete the sample analyzer from your resource. In typical usage scenarios, you would analyze multiple files using the same analyzer.</remarks>
        /// </summary>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>
        Task DeleteAnalyzerAsync(string analyzerId);
    }
}
