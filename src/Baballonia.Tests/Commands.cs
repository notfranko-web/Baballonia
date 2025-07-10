using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public class Commands
    {
        public static string ScanWifiNetworks()
        {
            return $@"{{""commands"": [{{""command"": ""scan_networks""}}]}}";
        }
        public static string GetWifiStatus()
        {
            return $@"{{""commands"": [{{""command"": ""get_wifi_status""}}]}}}}}}]}}";
        }
        public static string SetDataPaused(bool state)
        {
            return $@"{{""commands"": [{{""command"": ""pause"", ""data"": {{""pause"": {state.ToString().ToLower()}}}}}]}}";
        }
    }
}
