using System.Text.Json.Serialization;

namespace ContentUnderstanding.Common.Models
{
    public class PersonResponse
    {
        [JsonPropertyName("personId")]
        public string? PersonId { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("faceIds")]
        public List<string> FaceIds { get; set; } = new List<string>();
    }
}
