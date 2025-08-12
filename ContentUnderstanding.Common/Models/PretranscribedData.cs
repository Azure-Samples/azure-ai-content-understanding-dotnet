using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common.Models
{
    public class PretranscribedData
    {
        public List<Segment> Segments { get; set; } = [];
    }

    public class Segment
    {
        public float Start { get; set; }

        public float End { get; set; }

        public string? Text { get; set; }
    }
}
