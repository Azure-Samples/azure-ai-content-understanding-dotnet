namespace ContentUnderstanding.Common.Models
{
    public class AnalyzerDetail
    {
        public string AnalyzerId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public AnalyzerCapability[] Capabilities { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }
}
