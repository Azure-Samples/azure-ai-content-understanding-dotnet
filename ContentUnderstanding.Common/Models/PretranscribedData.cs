namespace ContentUnderstanding.Common.Models
{
    // Predefined data model for pretranscribed JSON
    public class PretranscribedData
    {
        public Segment[] Segments { get; set; }

        public class Segment
        {
            public float Start { get; set; }
            public float End { get; set; }
            public string Text { get; set; }
        }
    }
}
