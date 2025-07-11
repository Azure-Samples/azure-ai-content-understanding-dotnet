using System.Text.Json;

namespace ConversationalFieldExtraction.Extensions.Processor
{
    public class BatchTranscriptionProcessor : TranscriptProcessorBase
    {
        public BatchTranscriptionProcessor()
        {
            Name = "BatchTranscriptionProcessor";
        }

        public override JsonElement.ArrayEnumerator GetPhrases(JsonElement transcriptResult)
        {
            return transcriptResult.GetProperty("recognizedPhrases").EnumerateArray();
        }

        public override string FormatTimestamp(long time)
        {
            const long ticksPerMs = 10000;
            long milliseconds = time / ticksPerMs;

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
                long offsetInTicks = phrase.GetProperty("offsetInTicks").GetInt64();
                long durationInTicks = phrase.GetProperty("durationInTicks").GetInt64();
                long endInTicks = offsetInTicks + durationInTicks;

                string startTime = FormatTimestamp(offsetInTicks);
                string endTime = FormatTimestamp(endInTicks);

                var nBest = phrase.GetProperty("nBest")[0];
                string? text = nBest.GetProperty("display").GetString();
                var speaker = phrase.GetProperty("speaker").GetInt32();

                webvttLines.Add($"{startTime} --> {endTime}");
                webvttLines.Add($"<v Speaker {speaker}>{text}");
                webvttLines.Add("");
            }

            return string.Join("\n", webvttLines);
        }
    }
}
