using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common.Models
{
    public class ReferenceDocItem
    {
        public string Filename { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ResultFileName { get; set; } = string.Empty;
        public string ResultFilePath { get; set; } = string.Empty;
    }
}
