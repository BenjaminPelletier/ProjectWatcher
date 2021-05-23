using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectWatcher
{
    class Note
    {
        public DateTime Timestamp;
        public string Value;

        public override string ToString()
        {
            return Value;
        }
    }
}
