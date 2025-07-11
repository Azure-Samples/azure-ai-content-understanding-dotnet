using System.Text.Json.Serialization;

namespace BuildPersonDirectory.Models
{
    public class PersonIdentificationResponse
    {
        [JsonPropertyName("personCandidates")]
        public List<PersonCandidate> PersonCandidates { get; set; } = new List<PersonCandidate>();
    }
}
