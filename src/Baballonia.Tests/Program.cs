using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Baballonia.Tests;

public class Program
{
    public static void Main1(string[] args)
    {
        Console.WriteLine("ESP32-S3 Wi-Fi Tool");
        Console.WriteLine("-------------------------");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogging((loggingBuilder) => loggingBuilder
            .AddConsole()
            .AddDebug()
            .SetMinimumLevel(LogLevel.Trace));


        services.AddSingleton<ICommandSenderFactory, CommandSenderFactory>();
        services.AddTransient<FirmwareService>();

        var serviceProvider = services.BuildServiceProvider();
        var _logger = serviceProvider.GetRequiredService<ILogger<Program>>()!;

        FirmwareService firmwareService = serviceProvider.GetService<FirmwareService>()!;

        try
        {
            string[] goodPorts = { "COM4" }; //firmwareService.ProbeComPorts(TimeSpan.FromSeconds(10));
            string selectedPort = "";

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
                _logger.LogInformation("Multiple suitable devices were found: {}",
                    (object)goodPorts); // https://stackoverflow.com/questions/66317482/serilog-only-logs-first-element-of-array
                var res = AskUser("Multiple suitable devices were found", goodPorts);
                selectedPort = res.selectedOption;
            }


            var session = firmwareService.StartSession(CommandSenderType.Serial, selectedPort);
            session.WaitForHeartbeat();
            session.SendCommand(new FirmwareRequests.SetPausedRequest(true));

            string[] menu =
            {
                "Scan for WiFi networks",
                "Show available networks",
                "Configure WiFi",
                "Check WiFi status",
                "Connect to WiFi",
                "Start streaming mode",
                "Switch device mode",
                "Exit"
            };

            List<FirmwareResponses.WifiNetwork> scanResult = null;
            bool shouldRun = true;
            while (shouldRun)
            {
                var selection = AskUser("Setup Options", menu);
                switch (selection.index)
                {
                    case 0:
                        while (true)
                        {
                            var scanres = session.SendCommand(new FirmwareRequests.ScanWifiRequest());
                            if (scanres is not null)
                            {
                                scanResult = scanres.Networks;
                                scanResult = scanResult.OrderByDescending(n => n.Rssi).ToList();
                                break;
                            }

                            _logger.LogWarning("Networks not found, retrying...");
                        }

                        _logger.LogInformation("Found {} networks", scanResult.Count);
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
                        Console.WriteLine("{0,-25} {1,7} {2,6} {3,-20} {4,10}", "SSID", "Channel", "RSSI",
                            "MAC Address", "Auth Mode");
                        Console.WriteLine(new string('-', 75));
                        var selectedRow = AskUser("Enter network number", options);

                        var pwd = AskUser("Enter WiFi password");

                        session.SendCommand(
                            new FirmwareRequests.SetWifiRequest(scanResult[selectedRow.index].Ssid, pwd));
                        break;
                    case 3:
                        var res = session.SendCommand(new FirmwareRequests.GetWifiStatusRequest());
                        if (res == null)
                        {
                            Console.WriteLine($"WiFi Status: none");
                            break;
                        }

                        Console.WriteLine($"WiFi Status: {res.Status}");
                        Console.WriteLine($"Networks configured: {res.NetworksConfigured}");
                        Console.WriteLine($"IP Address: {res.IpAddress}");
                        break;
                    case 4:
                        session.SendCommand(new FirmwareRequests.ConnectWifiRequest());
                        break;
                    case 5:
                        session.SendCommand(new FirmwareRequests.StartStreamingRequest());
                        break;
                    case 6:
                        FirmwareRequests.Mode[] deviceModeOptions =
                        {
                            FirmwareRequests.Mode.Wifi,
                            FirmwareRequests.Mode.UVC,
                            FirmwareRequests.Mode.Auto
                        };
                        string[] modeOptions =
                        {
                            "WiFi - Stream over WiFi connection",
                            "UVC - Stream as USB webcam",
                            "Auto - Automatic mode selection"
                        };
                        var selectedMode = AskUser("Select new device mode", modeOptions);
                        session.SendCommand(new FirmwareRequests.SetModeRequest(deviceModeOptions[selectedMode.index]));
                        break;
                    case 7:
                        shouldRun = false;
                        break;
                }
            }

            session.SendCommand(new FirmwareRequests.SetPausedRequest(false));
            session.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error : {}", ex);
        }

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
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

    public static void PrintWifiScanResult(List<FirmwareResponses.WifiNetwork> result)
    {
        // Print header
        Console.WriteLine("{0,-25} {1,7} {2,6} {3,-20} {4,10}", "SSID", "Channel", "RSSI", "MAC Address", "Auth Mode");
        Console.WriteLine(new string('-', 75));

        // Print each network
        foreach (var network in result)
        {
            Console.WriteLine("{0,-25} {1,7} {2,6} {3,-20} {4,10}",
                network.Ssid,
                network.Channel,
                network.Rssi,
                network.MacAddress,
                network.AuthMode);
        }
    }

    public static string[] GetWifiScanResultLines(List<FirmwareResponses.WifiNetwork> result)
    {
        var lines = new List<string>();

        foreach (var network in result)
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
