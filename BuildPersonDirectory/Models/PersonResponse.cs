using System.Text.Json.Serialization;

namespace BuildPersonDirectory.Models
{
    public class PersonResponse
    {
        [JsonPropertyName("personId")]
        public string PersonId { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; }

        [JsonPropertyName("faceIds")]
        public List<string> FaceIds { get; set; }
    }
}
