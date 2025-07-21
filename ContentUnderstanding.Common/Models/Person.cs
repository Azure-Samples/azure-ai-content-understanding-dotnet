using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentUnderstanding.Common.Models
{
    public class Person
    {
        public string? PersonId { get; set; }

        public string? Name { get; set; }

        public List<string> Faces { get; set; } = new List<string>();
    }
}
