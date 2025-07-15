using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common
{
    public class ContentUnderstandingOptions
    {
        public string Endpoint { get; set; } = "";

        public string ApiVersion { get; set; } = "";

        public string SubscriptionKey { get; set; } = "";

        public string UserAgent { get; set; } = "";

        public string TrainingDataSasUrl { get; set; } = "";

        public string TrainingDataPath { get; set; } = "";

    }
}
