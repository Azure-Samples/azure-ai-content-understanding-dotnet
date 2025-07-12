using ContentUnderstanding.Common;
using ConversationalFieldExtraction.Extensions.Processor;
using ConversationalFieldExtraction.Interfaces;
using System.Text.Json;

namespace ConversationalFieldExtraction.Services
{
    public class ConversationalFieldExtractionService : IConversationalFieldExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;
        private readonly string OutputPath = "./outputs/conversational_field_extraction/";

        public ConversationalFieldExtractionService(AzureContentUnderstandingClient client)
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
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must not be null or empty.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template. This value must point to a valid template file and must not be null or
        /// empty.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CreateAnalyzerFromTemplateAsync(string analyzerId, string analyzerTemplatePath)
        {
            Console.WriteLine($"Creating Analyzer from Template: {analyzerId}...");

            try
            {
                var createResponse = await _client.BeginCreateAnalyzerAsync(
                    analyzerId: analyzerId,
                    analyzerTemplatePath: analyzerTemplatePath
                );

                // Poll for creation result
                var resultJson = await _client.PollResultAsync(createResponse);
                var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });

                Console.WriteLine("\nAnalyzer creation result:");
                Console.WriteLine(serializedJson);

                var output = $"{Path.Combine(OutputPath, $"{nameof(CreateAnalyzerFromTemplateAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
                await File.WriteAllTextAsync(output, serializedJson);

                Console.WriteLine($"Document Extraction has been saved to the output file path: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating analyzer: {ex.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Extract Fields Using the Analyzer
        /// <remarks>After the analyzer is successfully created, we can use it to analyze our input files.</remarks>
        /// </summary>
        /// <param name="analyzerId"></param>
        /// <param name="sampleFilePath"></param>
        /// <returns></returns>
        public async Task ExtractFieldsWithAnalyzerAsync(string analyzerId, string sampleFilePath)
        {
            Console.WriteLine("\nExtracting Fields with Analyzer...");

            try
            {
                // Convert to WebVTT format
                var (webvttFile, webvttFilePath) = ConvertFile(sampleFilePath);

                if (!IsValidWebVtt(webvttFilePath))
                {
                    Console.WriteLine("Error: The output is not in WebVTT format.");
                    return;
                }

                Console.WriteLine($"Using pretranscribed file: {webvttFilePath}");

                // Analyze with custom analyzer
                var response = await _client.BeginAnalyzeAsync(analyzerId, webvttFilePath);
                Console.WriteLine($"\nAnalysis response: {response.StatusCode}");

                var resultJson = await _client.PollResultAsync(response);
                var serializedJson = JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true });
                var output = $"{Path.Combine(OutputPath, $"{nameof(CreateAnalyzerFromTemplateAsync)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json")}";
                await File.WriteAllTextAsync(output, serializedJson);

                Console.WriteLine($"Document Extraction has been saved to the output file path: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during field extraction: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean Up
        /// <remarks>Optionally, delete the sample analyzer from your resource. In typical usage scenarios, you would analyze multiple files using the same analyzer.</remarks>
        /// </summary>
        /// <param name="analyzerId">The unique identifier of the analyzer to delete. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous delete operation.</returns>

        public async Task DeleteAnalyzerAsync(string analyzerId)
        {
            Console.WriteLine("\nCleaning Up Analyzer...");

            try
            {
                await _client.DeleteAnalyzerAsync(analyzerId);
                Console.WriteLine($"Analyzer {analyzerId} deleted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting analyzer: {ex.Message}");
            }
        }

        public (string convertedText, string convertedTextFilePath) ConvertFile(string filePath)
        {
            string convertedText = string.Empty;
            string convertedTextFilePath = string.Empty;

            dynamic transcripts = LoadTranscriptionFromLocal(filePath);
            string jsonString = JsonSerializer.Serialize(transcripts);
            var processor = new TranscriptsProcessor();

            if (jsonString.Contains("combinedRecognizedPhrases"))
            {
                Console.WriteLine("Processing a batch transcription file.");
                convertedText = processor.ConvertBTtoWebVTT(transcripts);
                convertedTextFilePath = SaveConvertedFile(convertedText, filePath);
            }
            else if (jsonString.Contains("combinedPhrases"))
            {
                Console.WriteLine("Processing a fast transcription file.");
                convertedText = processor.ConvertFTtoWebVTT(transcripts);
                convertedTextFilePath = SaveConvertedFile(convertedText, filePath);
            }
            else if (jsonString.Contains("WEBVTT"))
            {
                Console.WriteLine("Processing a CU transcription file.");
                convertedText = processor.ExtractCUWebVTT(transcripts);
                convertedTextFilePath = SaveConvertedFile(convertedText, filePath);
            }
            else
            {
                Console.WriteLine("No supported conversation transcription found. Skipping conversion.");
            }

            return (convertedText, convertedTextFilePath);
        }

        private dynamic LoadTranscriptionFromLocal(string filePath)
        {
            string content = File.ReadAllText(filePath);
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (JsonException)
            {
                return content;
            }
        }
        private string SaveConvertedFile(string content, string originalPath)
        {
            string newPath = Path.ChangeExtension(originalPath, "vtt");
            File.WriteAllText(newPath, content);
            return newPath;
        }

        public bool IsValidWebVtt(string filePath)
        {
            var content = File.ReadAllText(filePath);
            return content.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase);
        }

        public string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
        }

    }
}
