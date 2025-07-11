using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Baballonia.Tests;

public class Program
{
    public class WifiNetwork
    {
        [JsonPropertyName("ssid")]
        public string Ssid { get; set; }

        [JsonPropertyName("channel")]
        public int Channel { get; set; }

        [JsonPropertyName("rssi")]
        public int Rssi { get; set; }

        [JsonPropertyName("mac_address")]
        public string MacAddress { get; set; }

        [JsonPropertyName("auth_mode")]
        public int AuthMode { get; set; }
    }
    public class WifiScanResult
    {
        [JsonPropertyName("networks")]
        public List<WifiNetwork> Networks { get; set; }
    }
    public class WifiStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("networks_configured")]
        public int NetworksConfigured { get; set; }
        [JsonPropertyName("ip_address")]
        public string IpAddress { get; set; }

    }

    public class ResultWrapper
    {
        [JsonPropertyName("result")]
        public string Result { get; set; }
    }

    public class OuterResponse
    {
        [JsonPropertyName("results")]
        public List<string> Results { get; set; }
    }


    static void Main(string[] args)
    {
        Console.WriteLine("ESP32-S3 Wi-Fi Tool");
        Console.WriteLine("-------------------------");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogging((loggingBuilder) => loggingBuilder
            .AddConsole()
            .AddDebug()
            .SetMinimumLevel(LogLevel.Trace));


        services.AddSingleton<CommandSenderFactory>();
        services.AddTransient<FirmwareService>();

        var serviceProvider = services.BuildServiceProvider();
        var _logger = serviceProvider.GetRequiredService<ILogger<Program>>()!;

        FirmwareService firmwareService = serviceProvider.GetService<FirmwareService>()!;

        try
        {
            string[] goodPorts = { "COM4" }; //firmwareService.ProbeComPorts(TimeSpan.FromSeconds(10));
            string selectedPort = "COM4";

            if (goodPorts.Length < 1)
            {
                throw new Exception("No suitable ports found");
            }
            if (goodPorts.Length == 1)
            {
                selectedPort = goodPorts[0];
                _logger.LogInformation("Found one suitable device on {}", selectedPort);
            }
            else
            {
                _logger.LogInformation("Multiple suitable devices were found: {}", (object)goodPorts); // https://stackoverflow.com/questions/66317482/serilog-only-logs-first-element-of-array
                var res = AskUser("Multiple suitable devices were found", goodPorts);
                selectedPort = res.selectedOption;
            }


            firmwareService.StartSession(selectedPort);
            firmwareService.WaitForHeartbeat();
            firmwareService.SetIsDataPaused(true);

            string[] menu = {
                "Scan for WiFi networks",
                "Show available networks",
                "Configure WiFi",
                "Check WiFi status",
                "Connect to WiFi",
                "Start streaming mode",
                "Switch device mode",
                "Exit"
            };

            WifiScanResult? scanResult = null;
            bool shouldRun = true;
            while (shouldRun)
            {
                var selection = AskUser("Setup Options", menu);
                switch (selection.index)
                {
                    case 0:
                        JsonDocument? networks = null;
                        while (true)
                        {
                            networks = firmwareService.ScanForWifiNetworks();
                            if (networks is not null)
                                break;

                            _logger.LogWarning("Networks not found, retrying...");
                        }
                        _logger.LogInformation("Found networks: {}", networks.RootElement.GetRawText());
                        scanResult = networks.RootElement.Deserialize<WifiScanResult>();
                        scanResult.Networks = scanResult.Networks.OrderByDescending(n => n.Rssi).ToList();
                        break;
                    case 1:
                        if (scanResult is null)
                        {
                            Console.WriteLine("Run scan first");
                            break;
                        }
                        PrintWifiScanResult(scanResult);

                        break;
                    case 2:
                        var options = GetWifiScanResultLines(scanResult);
                        Console.WriteLine("{0,-25} {1,7} {2,6} {3,-20} {4,10}", "SSID", "Channel", "RSSI", "MAC Address", "Auth Mode");
                        Console.WriteLine(new string('-', 75));
                        var selectedRow = AskUser("Enter network number", options);

                        var pwd = AskUser("Enter WiFi password");

                        firmwareService.SetWifi(scanResult.Networks[selectedRow.index].Ssid, pwd);
                        break;
                    case 3:
                        // this is stupid and inconsistent
                        // TODO: ask Summer to fix this
                        var wifiStatusJson = firmwareService.GetWifiStatus();
                        var wifiStatusResults = wifiStatusJson.RootElement.Deserialize<OuterResponse>();
                        var wifiStatusWrapper = JsonSerializer.Deserialize<ResultWrapper>(wifiStatusResults.Results.First());
                        var wifiStatus = JsonSerializer.Deserialize<WifiStatus>(wifiStatusWrapper.Result);

                        Console.WriteLine($"WiFi Status: {wifiStatus.Status}");
                        Console.WriteLine($"Networks configured: {wifiStatus.NetworksConfigured}");
                        Console.WriteLine($"IP Address: {wifiStatus.IpAddress}");

                        break;
                    case 4:
                        firmwareService.ConnectWifi();
                        break;
                    case 5:
                        firmwareService.StartStreaming();
                        break;
                    case 6:
                        Commands.Mode[] deviceModeOptions = {
                            Commands.Mode.Wifi,
                            Commands.Mode.UVC,
                            Commands.Mode.Auto
                        };
                        string[] modeOptions =
                        {
                            "WiFi - Stream over WiFi connection",
                            "UVC - Stream as USB webcam",
                            "Auto - Automatic mode selection"
                        };
                        var selectedMode = AskUser("Select new device mode", modeOptions);
                        firmwareService.SetDeviceMode(deviceModeOptions[selectedMode.index]);
                        break;
                    case 7:
                        shouldRun = false;
                        break;

                }
            }

            firmwareService.SetIsDataPaused(false);
            firmwareService.StopSession();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error : {}", ex.Message);
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static string AskUser(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine("Please type:");
        return Console.ReadLine();
    }

    static (string selectedOption, int index) AskUser(string message, string[] options)
    {
        Console.WriteLine(message);
        Console.WriteLine("Please select an option:");

        // Display numbered options
        for (int i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {options[i]}");
        }
        int selectedIndex = -1;
        while (true)
        {
            Console.Write("Enter the number of your choice: ");
            string input = Console.ReadLine();

            if (int.TryParse(input, out selectedIndex) &&
                selectedIndex >= 1 &&
                selectedIndex <= options.Length)
            {
                break;
            }

            Console.WriteLine("Invalid input. Try again.");
        }

        return (options[selectedIndex - 1], selectedIndex - 1);


    }

    public static void PrintWifiScanResult(WifiScanResult result)
    {
        // Print header
        Console.WriteLine("{0,-25} {1,7} {2,6} {3,-20} {4,10}", "SSID", "Channel", "RSSI", "MAC Address", "Auth Mode");
        Console.WriteLine(new string('-', 75));

        // Print each network
        foreach (var network in result.Networks)
        {
            Console.WriteLine("{0,-25} {1,7} {2,6} {3,-20} {4,10}",
                network.Ssid,
                network.Channel,
                network.Rssi,
                network.MacAddress,
                network.AuthMode);
        }
    }
    public static string[] GetWifiScanResultLines(WifiScanResult result)
    {
        var lines = new List<string>();

        foreach (var network in result.Networks)
        {
            lines.Add(string.Format("{0,-25} {1,7} {2,6} {3,-20} {4,10}",
                network.Ssid,
                network.Channel,
                network.Rssi,
                network.MacAddress,
                network.AuthMode));
        }

        return lines.ToArray();
    }

}
