using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetricProvider.models
{
    public class ReadResult
    {
        public IList<string> keys { get; set; }
        public IList<List<string>> values { get; set; }
    }
}
