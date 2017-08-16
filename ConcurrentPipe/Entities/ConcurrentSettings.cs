using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConcurrentPipe.Entities
{
    public class ConcurrentSettings
    {
        public Dictionary<string, RunnerSetting> Runners { get; set; }
    }

    public class RunnerSetting
    {
        public int Timeout { get; set; }
        public List<string> Commands { get; set; }
    }
}
