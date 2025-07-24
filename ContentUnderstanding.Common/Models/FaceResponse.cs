using System.Text.Json.Serialization;

namespace ContentUnderstanding.Common.Models
{
    public class FaceResponse
    {
        [JsonPropertyName("faceId")]
        public string? FaceId { get; set; }

        [JsonPropertyName("personId")]
        public string? PersonId { get; set; }
    }
}
