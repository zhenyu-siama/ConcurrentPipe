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
        public string Name { get; set; }
        public int Timeout { get; set; } = 100;
        public List<string> Commands { get; set; }
        public List<string> ReadyChecks { get; set; }
        public int ReadyTimeout { get; set; } = 100;
        public List<string> Alives { get; set; }
        public List<RunnerSetting> Runners { get; set; }
        public string Directory { get; set; }
    }
}
