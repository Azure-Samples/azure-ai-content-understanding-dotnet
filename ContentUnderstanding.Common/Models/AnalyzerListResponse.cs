using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentUnderstanding.Common.Models
{
    public class AnalyzerListResponse
    {
        [JsonPropertyName("value")]
        public JsonElement[]? Value { get; set; }
    }
}
