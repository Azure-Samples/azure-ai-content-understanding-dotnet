namespace ContentUnderstanding.Common.Models
{
    public class AnalyzerOperationResponse
    {
        public string Status { get; set; }
        public AnalyzerCreationResult Result { get; set; }
        public AnalyzerError Error { get; set; }
    }
}
