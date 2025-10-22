using Azure;
using Azure.AI.ContentUnderstanding;
using ContentUnderstanding.Common;
using FieldExtraction.Interfaces;
using System.Text.Json;

namespace FieldExtraction.Services
{
    public class FieldExtractionService : IFieldExtractionService
    {
        private readonly ContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/field_extraction/";

        public FieldExtractionService(ContentUnderstandingClient client) 
        { 
            _client = client;

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }
        }

        /// <summary>
        /// Create Analyzer from the Template.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must be non-null and unique  within the system.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template used for creating the analyzer. The path must point to  a valid template
        /// file.</param>
        /// <param name="sampleFilePath">The file path to the sample file to be analyzed. The path must point to a valid file that can  be processed by
        /// the analyzer.</param>
        /// <returns></returns>
        public async Task<AnalyzeResult> CreateAndUseAnalyzer(string analyzerId, ContentAnalyzer analyzer, string fileName)
        {
            Console.WriteLine($"🔧 Creating custom analyzer '{analyzerId}'...");

            // Start the create or replace operation
            var analyzerOperation = await _client.GetContentAnalyzersClient()
                .CreateOrReplaceAsync(
                    waitUntil: WaitUntil.Completed,
                    analyzerId: analyzerId,
                    resource: analyzer);

            // Get the result
            ContentAnalyzer result = analyzerOperation.Value;
            Console.WriteLine($"✅ Analyzer '{analyzerId}' created successfully!");
            Console.WriteLine($"   Status: {result.Status}");
            Console.WriteLine($"   Created At: {result.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"   Base Analyzer: {result.BaseAnalyzerId}");
            Console.WriteLine($"   Description: {result.Description}");

            // Display field schema information
            if (result.FieldSchema != null)
            {
                Console.WriteLine($"\n📋 Field Schema: {result.FieldSchema.Name}");
                Console.WriteLine($"   {result.FieldSchema.Description}");
                Console.WriteLine($"   Fields:");
                foreach (var field in result.FieldSchema.Fields)
                {
                    Console.WriteLine($"      - {field.Key}: {field.Value.Type} ({field.Value.Method})");
                    Console.WriteLine($"        {field.Value.Description}");
                }
            }

            // Display any warnings
            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                Console.WriteLine($"\n⚠️  Warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"      - {warning.Code}: {warning.Message}");
                }
            }

            // Read file from disk
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"❌ Error: Sample file not found at {fileName}");
                throw new FileNotFoundException("Sample file not found.", fileName);
            }

            byte[] bytes = await File.ReadAllBytesAsync(fileName);
            Console.WriteLine($"\n📄 Analyzing file '{Path.GetFileName(fileName)}'...");

            // Start the analyze operation with binary content
            BinaryData binaryData = BinaryData.FromBytes(bytes);
            var analyzeOperation = await _client.GetContentAnalyzersClient()
                .AnalyzeAsync(
                    waitUntil: WaitUntil.Completed,
                    analyzerId: analyzerId,
                    data: binaryData);

            // Get the result
            AnalyzeResult analyzeResult = analyzeOperation.Value;

            // Display markdown content
            Console.WriteLine("\n📄 Markdown Content:");
            Console.WriteLine("=" + new string('=', 49));
            Console.WriteLine("\n✅ Analysis complete!");

            // Clean up the created analyzer
            Console.WriteLine($"\n🗑️  Deleting analyzer '{analyzerId}'...");
            await _client.GetContentAnalyzersClient().DeleteAsync(analyzerId);
            Console.WriteLine($"✅ Analyzer '{analyzerId}' deleted successfully!");

            Console.WriteLine("\n💡 Next steps:");
            Console.WriteLine("   - To retrieve an analyzer: see GetAnalyzer sample");
            Console.WriteLine("   - To use the analyzer for analysis: see AnalyzeBinary sample");
            Console.WriteLine("   - To delete an analyzer: see DeleteAnalyzer sample");

            return analyzeResult;
        }

        public string GetFileTypeDescription(string extension) => extension switch
        {
            ".pdf" => "PDF Document",
            ".mp3" => "Audio Recording",
            ".wav" => "Audio Recording",
            ".mp4" => "Video File",
            ".mov" => "Video File",
            _ => "Unknown File Type"
        };
    }
}
