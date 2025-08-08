﻿using ContentUnderstanding.Common;
using ContentUnderstanding.Common.Models;
using ConversationalFieldExtraction.Interfaces;
using System.Text;
using System.Text.Json;

namespace ConversationalFieldExtraction.Services
{
    /// <summary>
    /// Service for performing conversational field extraction operations using Azure Content Understanding.
    /// Provides functionality to create analyzers from templates, extract structured fields from conversation data,
    /// and manage analyzer lifecycle including cleanup operations.
    /// </summary>
    public class ConversationalFieldExtractionService : IConversationalFieldExtractionService
    {
        private readonly AzureContentUnderstandingClient _client;

        public ConversationalFieldExtractionService(AzureContentUnderstandingClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Creates a new analyzer from a specified template file and polls for the completion of the creation operation.
        /// This method initiates the analyzer creation process using the Azure Content Understanding service and waits
        /// for the operation to complete before returning. The analyzer can then be used for conversational field extraction.
        /// </summary>
        /// <param name="analyzerId">The unique identifier for the analyzer to be created. This value must not be null or empty.</param>
        /// <param name="analyzerTemplatePath">The file path to the analyzer template. This value must point to a valid template file and must not be null or
        /// empty.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<JsonDocument> CreateAnalyzerFromTemplateAsync(string analyzerId, string analyzerTemplatePath)
        {
            Console.WriteLine($"===== Creating Analyzer from Template: {analyzerId} =====");

            try
            {
                var response = await _client.BeginCreateAnalyzerAsync(
                    analyzerId: analyzerId,
                    analyzerTemplatePath: analyzerTemplatePath
                );

                // Poll for creation result
                JsonDocument resultJson = await _client.PollResultAsync(response);
                Console.WriteLine("\nAnalyzer creation result:");
                Console.WriteLine(JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true }));

                return resultJson;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating analyzer: {ex.Message}");
                throw;
            }
        }

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
        public async Task<JsonDocument?> ExtractFieldsWithAnalyzerAsync(string analyzerId, string filePath)
        {
            Console.WriteLine("\n===== Extracting Fields with Analyzer =====");

            try
            {
                // Convert to WebVTT format
                var webvttFilePath = ConvertToWebVtt(filePath);

                if (!IsValidWebVtt(webvttFilePath))
                {
                    Console.WriteLine("Error: The output is not in WebVTT format.");
                    return null;
                }

                Console.WriteLine($"Using pretranscribed file: {webvttFilePath}");

                // Analyze with custom analyzer
                var response = await _client.BeginAnalyzeAsync(analyzerId, webvttFilePath);
                Console.WriteLine($"\nAnalysis response: {response.StatusCode}");

                var resultJson = await _client.PollResultAsync(response);

                Console.WriteLine("\nExtraction Results:");
                Console.WriteLine(JsonSerializer.Serialize(resultJson, new JsonSerializerOptions { WriteIndented = true }));

                return resultJson;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during field extraction: {ex.Message}");
                throw;
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
            Console.WriteLine("\n===== Cleaning Up Analyzer =====");

            try
            {
                await _client.DeleteAnalyzerAsync(analyzerId);
                Console.WriteLine($"Analyzer {analyzerId} deleted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting analyzer: {ex.Message}");
                throw;
            }
        }

        private string ConvertToWebVtt(string jsonFilePath)
        {
            // Read and parse JSON file
            var jsonContent = File.ReadAllText(jsonFilePath);
            var transcriptData = JsonSerializer.Deserialize<PretranscribedData>(jsonContent) ?? new PretranscribedData();

            // Create WebVTT content
            var webvtt = new StringBuilder();
            webvtt.AppendLine("WEBVTT");
            webvtt.AppendLine();

            for (int i = 0; i < transcriptData.Segments.Count; i++)
            {
                var segment = transcriptData.Segments[i];
                webvtt.AppendLine($"{i + 1}");
                webvtt.AppendLine($"{FormatTime(segment.Start)} --> {FormatTime(segment.End)}");
                webvtt.AppendLine(segment.Text);
                webvtt.AppendLine();
            }

            // Save to file
            var webvttFilePath = Path.ChangeExtension(jsonFilePath, ".vtt");
            File.WriteAllText(webvttFilePath, webvtt.ToString());
            return webvttFilePath;
        }

        private bool IsValidWebVtt(string filePath)
        {
            var content = File.ReadAllText(filePath);
            return content.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase);
        }

        private string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
        }
    }
}
