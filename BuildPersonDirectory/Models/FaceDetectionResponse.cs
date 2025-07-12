using System.Text.Json.Serialization;

namespace BuildPersonDirectory.Models
{
    public class FaceDetectionResponse
    {
        [JsonPropertyName("detectedFaces")]
        public List<DetectedFace> DetectedFaces { get; set; } = new List<DetectedFace>();
    }
}
