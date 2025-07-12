using System.Text.Json;

namespace ConversationalFieldExtraction.Extensions.Processor
{
    public class FastTranscriptionProcessor : TranscriptProcessorBase
    {
        public FastTranscriptionProcessor()
        {
            Name = "FastTranscriptionProcessor";
        }

        public override string FormatTimestamp(long milliseconds)
        {
            long seconds = milliseconds / 1000;
            long ms = milliseconds % 1000;
            long minutes = seconds / 60;
            seconds %= 60;
            long hours = minutes / 60;
            minutes %= 60;

            return $"{hours:00}:{minutes:00}:{seconds:00}.{ms:000}";
        }

        public override string ProcessTranscript(JsonElement transcriptResult)
        {
            var webvttLines = new List<string> { "WEBVTT" };

            var phrases = GetPhrases(transcriptResult);
            foreach (var phrase in phrases)
            {
                long offsetMs = phrase.GetProperty("offsetMilliseconds").GetInt64();
                long durationMs = phrase.GetProperty("durationMilliseconds").GetInt64();
                long endMs = offsetMs + durationMs;

                string startTime = FormatTimestamp(offsetMs);
                string endTime = FormatTimestamp(endMs);

                string? text = phrase.GetProperty("text").GetString();
                var speaker = phrase.GetProperty("speaker").GetInt32();

                webvttLines.Add($"{startTime} --> {endTime}");
                webvttLines.Add($"<v Speaker {speaker}>{text}");
                webvttLines.Add("");
            }

            return string.Join("\n", webvttLines);
        }
    }
}
