using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectWatcher
{
    public class CameraData
    {
        public readonly string Name;
        public readonly string Moniker;
        public readonly HashSet<string> StableImages = new HashSet<string>();

        public CameraData(string name, string moniker)
        {
            Name = name;
            Moniker = moniker;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
