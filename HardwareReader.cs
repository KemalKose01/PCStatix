using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using Hardware.Info;

namespace PCStatix.Classes
{
    public class HardwareReader
    {
        private readonly Computer computer;
        private readonly HardwareInfo hwInfo;

        public HardwareReader()
        {
            // CPU bilgi sağlayıcı
            hwInfo = new HardwareInfo();
            hwInfo.RefreshCPUList(true);

            // gpu ram anakart ve depolama sensörleri  
            computer = new Computer()
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true
            };

            computer.Open();
        }

        public SensorData GetCpuData()
        {
            hwInfo.RefreshCPUList(true);

            var temperatures = new List<double>();

            foreach (var cpu in hwInfo.CpuList)
            {
                // Reflection ile Temperature listesini yakala
                var tempProp = cpu.GetType().GetProperty("Temperature");
                if (tempProp == null) continue;

                var tempValue = tempProp.GetValue(cpu);
                if (tempValue == null) continue;

                if (tempValue is System.Collections.IEnumerable list)
                {
                    foreach (var tmp in list)
                    {
                        var val = tmp?.GetType().GetProperty("CurrentValue")?.GetValue(tmp);
                        if (val != null && double.TryParse(val.ToString(), out double result))
                        {
                            if (result > 0) temperatures.Add(result);
                        }
                    }
                }
                else
                {
                    var val = tempValue.GetType().GetProperty("CurrentValue")?.GetValue(tempValue);
                    if (val != null && double.TryParse(val.ToString(), out double result))
                    {
                        if (result > 0) temperatures.Add(result);
                    }
                }
            }

            double cpuTemp = temperatures.DefaultIfEmpty(0).Max();

            return new SensorData
            {
                Name = "CPU",
                Type = "CPU",
                Value = (float)cpuTemp,
                Unit = "°C"
            };
        }

        public SensorData GetGpuData()
        {
            foreach (var hardware in computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.GpuNvidia ||
                    hardware.HardwareType == HardwareType.GpuAmd)
                {
                    hardware.Update();

                    var temp = hardware.Sensors
                        .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                        .Select(s => s.Value.Value)
                        .DefaultIfEmpty(0)
                        .Max();

                    return new SensorData
                    {
                        Name = hardware.Name,
                        Type = "GPU",
                        Value = temp,
                        Unit = "°C"
                    };
                }
            }

            return null;
        }

        public List<SensorData> GetOtherSensors()
        {
            var list = new List<SensorData>();

            foreach (var hw in computer.Hardware)
            {
                hw.Update();

                foreach (var sensor in hw.Sensors)
                {
                    if (sensor.Value.HasValue &&
                        sensor.SensorType != SensorType.Temperature) // CPU temp’i tekrar göstermesin
                    {
                        list.Add(new SensorData
                        {
                            Name = sensor.Name,
                            Type = sensor.SensorType.ToString(),
                            Value = sensor.Value.Value,
                            Unit = sensor.SensorType.ToString()
                        });
                    }
                }
            }

            return list;
        }

        public void Close()
        {
            computer.Close();
        }
    }
}


