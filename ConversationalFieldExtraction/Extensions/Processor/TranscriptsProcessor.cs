using System.Text;
using System.Text.Json;

namespace ConversationalFieldExtraction.Extensions.Processor
{
    public class TranscriptsProcessor
    {
        private readonly Dictionary<string, TranscriptProcessorBase> _processors;

        public TranscriptsProcessor()
        {
            _processors = new Dictionary<string, TranscriptProcessorBase>
            {
                ["batch_transcription"] = new BatchTranscriptionProcessor(),
                ["fast_transcription"] = new FastTranscriptionProcessor(),
                ["cu_markdown"] = new CUTranscriptionProcessor()
            };
        }

        public TranscriptProcessorBase GetTranscriptionProcessor(string transcriptsType)
        {
            if (!_processors.ContainsKey(transcriptsType))
            {
                throw new ArgumentException($"'{transcriptsType}' is invalid");
            }
            return _processors[transcriptsType];
        }

        public JsonElement LoadTranscriptionFromLocal(string filePath)
        {
            string jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
            Console.WriteLine("Load transcription completed.");
            return JsonSerializer.Deserialize<JsonElement>(jsonContent);
        }

        public string ConvertBTtoWebVTT(JsonElement transcripts)
        {
            var processor = (BatchTranscriptionProcessor)GetTranscriptionProcessor("batch_transcription");
            var result = processor.ProcessTranscript(transcripts);
            Console.WriteLine("Batch to WebVTT Conversion completed.");
            return result ?? "";
        }

        public string ConvertFTtoWebVTT(JsonElement transcripts)
        {
            var processor = (FastTranscriptionProcessor)GetTranscriptionProcessor("fast_transcription");
            var result = processor.ProcessTranscript(transcripts);
            Console.WriteLine("Fast to WebVTT Conversion completed.");
            return result;
        }

        public string ExtractCUWebVTT(JsonElement transcripts)
        {
            var processor = GetTranscriptionProcessor("cu_markdown");
            var result = processor.ProcessTranscript(transcripts);
            Console.WriteLine("CU to WebVTT Conversion completed.");
            return result;
        }

        public (string convertedText, string convertedTextFilePath) ConvertFile(string filePath)
        {
            string convertedText = string.Empty;
            string convertedTextFilePath = string.Empty;

            JsonElement transcripts = LoadTranscriptionFromLocal(filePath);
            string transcriptsStr = transcripts.ToString();

            if (transcripts.TryGetProperty("combinedRecognizedPhrases", out _))
            {
                Console.WriteLine("Processing a batch transcription file.");
                convertedText = ConvertBTtoWebVTT(transcripts);
                convertedTextFilePath = SaveConvertedFile(convertedText, filePath);
            }
            else if (transcripts.TryGetProperty("combinedPhrases", out _))
            {
                Console.WriteLine("Processing a fast transcription file.");
                convertedText = ConvertFTtoWebVTT(transcripts);
                convertedTextFilePath = SaveConvertedFile(convertedText, filePath);
            }
            else if (transcriptsStr.Contains("WEBVTT"))
            {
                Console.WriteLine("Processing a CU transcription file.");
                convertedText = ExtractCUWebVTT(transcripts);
                convertedTextFilePath = SaveConvertedFile(convertedText, filePath);
            }
            else
            {
                Console.WriteLine("No supported conversation transcription found. Skipping conversion.");
            }

            return (convertedText, convertedTextFilePath);
        }

        public string SaveConvertedFile(string content, string originalPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(originalPath);
            string outputDir = Path.Combine("..", "data", "transcripts_processor_output");
            string tempFile = Path.Combine(outputDir, $"{fileName}.convertedTowebVTT.txt");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            try
            {
                File.WriteAllText(tempFile, content, Encoding.UTF8);
                Console.WriteLine($"Conversion completed. The result has been saved to '{tempFile}'");
                return tempFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during the conversion process: {ex.Message}");
                return "";
            }
        }
    }
}
