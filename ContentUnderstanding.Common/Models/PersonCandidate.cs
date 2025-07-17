using System.Text.Json.Serialization;

namespace ContentUnderstanding.Common.Models
{
    public class PersonCandidate
    {
        [JsonPropertyName("personId")]
        public string PersonId { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }
    }
}
