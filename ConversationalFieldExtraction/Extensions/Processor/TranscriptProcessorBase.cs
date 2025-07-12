using System.Text.Json;

namespace ConversationalFieldExtraction.Extensions.Processor
{
    public abstract class TranscriptProcessorBase
    {
        public string? Name { get; protected set; }

        public abstract string ProcessTranscript(JsonElement transcriptResult);

        public virtual JsonElement.ArrayEnumerator GetPhrases(JsonElement transcriptResult)
        {
            return transcriptResult.GetProperty("phrases").EnumerateArray();
        }

        public virtual string FormatTimestamp(long time)
        {
            return "";
        }
    }
}
