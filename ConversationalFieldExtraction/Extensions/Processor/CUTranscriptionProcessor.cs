using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConversationalFieldExtraction.Extensions.Processor
{
    public class CUTranscriptionProcessor : TranscriptProcessorBase
    {
        public CUTranscriptionProcessor()
        {
            Name = "CUTranscriptionProcessor";
        }

        public override string ProcessTranscript(JsonElement transcriptResult)
        {
            return transcriptResult
                .GetProperty("result")
                .GetProperty("contents")[0]
                .GetProperty("markdown")
                .GetString();
        }
    }
}
