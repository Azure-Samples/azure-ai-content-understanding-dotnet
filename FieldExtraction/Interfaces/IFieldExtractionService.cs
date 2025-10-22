using Azure.AI.ContentUnderstanding;
using System.Text.Json;

namespace FieldExtraction.Interfaces
{
    public interface IFieldExtractionService
    {
        /// <summary>
        /// Create Analyzer from the Template.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must be non-null and unique  within the system.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template used for creating the analyzer. The path must point to  a valid template
        /// file.</param>
        /// <param name="sampleFilePath">The file path to the sample file to be analyzed. The path must point to a valid file that can  be processed by
        /// the analyzer.</param>
        /// <returns></returns>
        Task<AnalyzeResult> CreateAndUseAnalyzer(string analyzerId, ContentAnalyzer analyzer, string fileName);
    }
}
