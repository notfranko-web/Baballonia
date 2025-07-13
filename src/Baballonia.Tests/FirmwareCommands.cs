using System.Text.Json;

namespace Baballonia.Tests
{
    public class FirmwareCommands
    {

        // No string enums, cope :(
        public class Mode
        {
            private Mode(string value)
            {
                Value = value;
            }

            public string Value { get; private set; }
            public static Mode Wifi { get { return new Mode("wifi"); } }
            public static Mode UVC { get { return new Mode("uvc"); } }
            public static Mode Auto { get { return new Mode("auto"); } }

        }

        public class CommandRoot
        {
            public List<Command> commands { get; }
            public CommandRoot(List<Command> commands)
            {
                this.commands = commands;
            }

            public string serialize()
            {
                return JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    UnknownTypeHandling = System.Text.Json.Serialization.JsonUnknownTypeHandling.JsonNode,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false
                });
            }
        }
        private interface Params { }
        public struct Command
        {
            public string? command { get; set; }
            // object instead of Params because implementing converters for every class is a pain
            // https://khalidabuhakmeh.com/serialize-interface-instances-system-text-json
            public object? data { get; set; }

            public Command() { }
            public Command(string command)
            {
                this.command = command;
            }
        }
        private struct WifiData : Params
        {
            public string name { get; set; }
            public string ssid { get; set; }
            public string password { get; set; }
            public int channel { get; set; }
            public int power { get; set; }
        }
        private struct DataPausedData : Params
        {
            public bool pause { get; set; }
        }
        private struct ModeData : Params
        {
            public string mode { get; set; }
        }

        public class CommandBuilder()
        {
            private List<Command> commands = new List<Command>();
            public CommandRoot build()
            {
                CommandRoot root = new CommandRoot(commands);
                return root;
            }
            public CommandBuilder SetDataPaused(bool state)
            {
                Command command = new Command("pause");
                command.data = new DataPausedData { pause = state };

                commands.Add(command);
                return this;
            }
            public CommandBuilder ScanWifiNetworks()
            {
                var command = new Command("scan_networks");

                commands.Add(command);
                return this;
            }
            public CommandBuilder SetWifi(string ssid, string password)
            {
                var command = new Command("set_wifi");
                command.data = new WifiData
                {
                    name = "main",
                    ssid = ssid,
                    password = password,
                    channel = 0,
                    power = 0
                };

                commands.Add(command);
                return this;
            }

            public CommandBuilder GetWifiStatus()
            {
                var command = new Command("get_wifi_status");

                commands.Add(command);
                return this;
            }
            public CommandBuilder ConnectWifi()
            {
                var command = new Command("connect_wifi");

                commands.Add(command);
                return this;
            }
            public CommandBuilder StartStreaming()
            {
                var command = new Command("start_streaming");

                commands.Add(command);
                return this;
            }
            public CommandBuilder GetDeviceMode()
            {
                var command = new Command("get_device_mode");

                commands.Add(command);
                return this;
            }
            public CommandBuilder SetStreamMode(Mode mode)
            {
                var command = new Command("switch_mode");
                command.data = new ModeData
                {
                    mode = mode.Value
                };

                commands.Add(command);
                return this;
            }
        }

        public static CommandBuilder Builder()
        {
            return new CommandBuilder();
        }
    }
}
