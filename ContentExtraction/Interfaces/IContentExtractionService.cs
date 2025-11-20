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
        Task<JsonDocument> AnalyzeDocumentAsync(string filePath);

        /// <summary>
        /// Analyzes a document from a specified URL using a prebuilt document analyzer.
        /// </summary>
        /// <remarks>This method performs an analysis of a document located at a predefined URL using a
        /// specific analyzer. The analysis extracts content such as markdown, document metadata, pages, and tables, and
        /// outputs the results to the console. The full analysis result is saved as a JSON file for further
        /// review.</remarks>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<JsonDocument> AnalyzeDocumentFromUrlAsync(string documentUrl);

        /// <summary>
        /// Analyzes the audio file at the specified file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        Task<JsonDocument> AnalyzeAudioAsync(string filePath);

        /// <summary>
        /// Analyzes a video file using a prebuilt video analyzer and processes the analysis results.
        /// </summary>
        /// <remarks>This method performs the following steps: <list type="bullet">
        /// <item><description>Initiates a video analysis operation using a specified analyzer.</description></item>
        /// <item><description>Polls for the completion of the analysis operation.</description></item>
        /// <item><description>Extracts and displays key information, such as markdown content, transcript phrases, and
        /// key frames.</description></item> <item><description>Saves the full analysis result to a JSON file and
        /// processes keyframe images if available.</description></item> </list> The method assumes the video file is
        /// located at a predefined path and uses a specific analyzer ID.</remarks>
        /// <returns></returns>
        Task<JsonDocument> AnalyzeVideoAsync(string filePath);
    }
}
