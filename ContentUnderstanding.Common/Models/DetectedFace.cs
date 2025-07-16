using System.Text.Json.Serialization;

namespace ContentUnderstanding.Common.Models
{
    public class DetectedFace
    {
        [JsonPropertyName("faceId")]
        public string FaceId { get; set; }

        [JsonPropertyName("boundingBox")]
        public Dictionary<string, object> BoundingBox { get; set; } = new Dictionary<string, object>();
    }
}
