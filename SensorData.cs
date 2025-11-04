using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hardware.Info;

namespace PCStatix.Classes
{
    public class SensorData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public float? Value { get; set; }
        public string Unit { get; set; }
    }
}
