using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public class Commands
    {
        private struct CommandRoot
        {
            public List<Command> commands { get; set; }
        }
        private struct Command
        {
            public string command { get; set; }
            public object data { get; set; }
        }
        private struct WifiData
        {
            public string name { get; set; }
            public string ssid { get; set; }
            public string password { get; set; }
            public int channel { get; set; }
            public int power { get; set; }
        }

        private struct DataPausedData
        {
            public bool pause { get; set; }
        }


        private static string serialize(object obj)
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            });
        }

        public static string SetDataPaused(bool state)
        {
            var command = new Command
            {
                command = "pause",
                data = new DataPausedData
                {
                    pause = state
                }
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };

            return serialize(root);
        }
        public static string ScanWifiNetworks()
        {
            var command = new Command
            {
                command = "scan_networks"
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };

            return serialize(root);
        }
        public static string SetWifi(string ssid, string password)
        {
            var command = new Command
            {
                command = "set_wifi",
                data = new WifiData
                {
                    name = "main",
                    ssid = ssid,
                    password = password,
                    channel = 0,
                    power = 0
                }
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };
            return serialize(root);
        }
        public static string GetWifiStatus()
        {
            var command = new Command
            {
                command = "get_wifi_status"
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };
            return serialize(root);
        }

        public static string ConnectWifi()
        {
            var command = new Command
            {
                command = "connect_wifi"
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };
            return serialize(root);
        }
        public static string StartStreaming()
        {
            var command = new Command
            {
                command = "start_streaming"
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };
            return serialize(root);
        }
        public static string GetDeviceMode()
        {
            var command = new Command
            {
                command = "get_device_mode"
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };
            return serialize(root);
        }
        public class Mode
        {
            private Mode(string value)      
            {
                Value = value;
            }

            public string Value {  get; private set; }
            public static Mode Wifi { get { return new Mode("wifi"); } }
            public static Mode UVC { get { return new Mode("uvc"); } }
            public static Mode Auto { get { return new Mode("auto"); } }

        }
        private struct ModeData
        {
            public string mode { get; set; }
        }
        public static string SwitchMode(Mode mode)
        {
            var command = new Command
            {
                command = "switch_mode",
                data = new ModeData
                {
                    mode = mode.Value
                }
            };
            CommandRoot root = new CommandRoot()
            {
                commands = new List<Command>() { command }
            };
            return serialize(root);
        }
    }
}
