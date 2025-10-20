using Azure.AI.ContentUnderstanding;
using System.Text.Json;

namespace ContentExtraction.Interfaces
{
    public interface IContentExtractionService
    {
        /// <summary>
        /// Document Content
        /// <remarks>Content Understanding API is designed to extract all textual content from a specified document file. 
        /// In addition to text extraction, it conducts a comprehensive layout analysis to identify and categorize tables and figures within the document. 
        /// The output is then presented in a structured markdown format, ensuring clarity and ease of interpretation.</remarks>
        /// </summary>
        /// <param name="filePath">The path to the document file to be analyzed. Must be a valid file path.</param>
        /// <returns>A task representing the asynchronous operation. The task completes when the document analysis is finished.</returns>
        Task<AnalyzeResult> AnalyzeDocumentAsync(string filePath);

        /// <summary>
        /// Analyzes the audio file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<AnalyzeResult> AnalyzeAudioAsync(string filePath);

        /// <summary>
        /// Analyzes the video file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<AnalyzeResult> AnalyzeVideoAsync(string filePath);

        /// <summary>
        /// Analyzes the video file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<AnalyzeResult> AnalyzeVideoWithFaceAsync(string filePath);
    }
}
