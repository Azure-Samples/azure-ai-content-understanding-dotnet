using System.Text.Json;

namespace FieldExtraction.Interfaces
{
    public interface IFieldExtractionService
    {
        /// <summary>
        /// Analyze a file using a prebuilt analyzer.
        /// </summary>
        /// <param name="prebuiltAnalyzerId">The prebuilt analyzer ID (e.g., "prebuilt-invoice", "prebuilt-receipt").</param>
        /// <param name="fileName">The file path to the sample file to be analyzed.</param>
        /// <param name="filenamePrefix">Prefix for the output file name.</param>
        /// <returns>The analysis result as a JsonDocument.</returns>
        Task<JsonDocument> AnalyzeWithPrebuiltAnalyzer(string prebuiltAnalyzerId, string fileName, string filenamePrefix);

        /// <summary>
        /// Create Analyzer and use it to analyze a file.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created.</param>
        /// <param name="analyzerDefinition">The analyzer definition as a dictionary (JSON structure).</param>
        /// <param name="fileName">The file path to the sample file to be analyzed.</param>
        /// <returns>The analysis result as a JsonDocument.</returns>
        Task<JsonDocument> CreateAndUseAnalyzer(string analyzerId, Dictionary<string, object> analyzerDefinition, string fileName);
    }
}
