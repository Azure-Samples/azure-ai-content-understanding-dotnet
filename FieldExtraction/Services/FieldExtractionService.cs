using ContentUnderstanding.Common;
using FieldExtraction.Interfaces;
using System.Text.Json;

namespace FieldExtraction.Services
{
    public class FieldExtractionService : IFieldExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/field_extraction/";

        public FieldExtractionService(AzureContentUnderstandingClient client) 
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
        public async Task CreateAndUseAnalyzer(string analyzerId, string analyzerTemplatePath, string sampleFilePath)
        {
            Console.WriteLine("Creating Analyzer...");
            Console.WriteLine($"Template: {Path.GetFileName(analyzerTemplatePath)}");
            Console.WriteLine($"Analyzer ID: {analyzerId}");

            // Create analyzer from template
            var createResponse = await _client.BeginCreateAnalyzerAsync(
                analyzerId: analyzerId,
                analyzerTemplatePath: analyzerTemplatePath
            );

            // Poll for creation result
            var createResult = await _client.PollResultAsync(createResponse);
            Console.WriteLine("\nAnalyzer created successfully");

            Console.WriteLine("\n===== Analyzing Sample File =====");
            Console.WriteLine($"Input file: {Path.GetFileName(sampleFilePath)}");

            // Extract Fields Using the Analyzer.
            // After the analyzer is successfully created, we can use it to analyze our input files.
            var analyzeResponse = await _client.BeginAnalyzeAsync(analyzerId, sampleFilePath);
            var analyzeResult = await _client.PollResultAsync(analyzeResponse);

            Console.WriteLine("\n===== Extraction Results =====");
            await PrintExtractionResultsAsync(analyzeResult, sampleFilePath);

            // // Optionally, delete the sample analyzer from your resource. In typical usage scenarios, you would analyze multiple files using the same analyzer.
            Console.WriteLine("\n===== Cleaning Up =====");
            await _client.DeleteAnalyzerAsync(analyzerId);
            Console.WriteLine($"Analyzer {analyzerId} deleted");
        }

        public async Task PrintExtractionResultsAsync(dynamic result, string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string fileName = Path.GetFileName(filePath);

            Console.WriteLine($"File: {fileName}");
            Console.WriteLine($"Type: {GetFileTypeDescription(extension)}");
            Console.WriteLine($"Analyzer completed at: {DateTime.Now}");
            Console.WriteLine("\nExtracted Fields:");

            var serializedJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var output = $"{Path.Combine(OutputPath, $"FieldExtraction_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
            await File.WriteAllTextAsync(output, serializedJson);

            Console.WriteLine($"Document Extraction has been saved to the output file path: {output}");
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
