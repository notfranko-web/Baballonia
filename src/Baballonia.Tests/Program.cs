using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace Baballonia.Tests;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("ESP32-S3 Wi-Fi Tool");
        Console.WriteLine("-------------------------");

        // Get serial port information
        // string port = GetSerialPort();
        string port = "COM4"; //mine is always COM4
        // const int baudRate = 921600;
        SerialCommandSender sender = new SerialCommandSender(port);
        FirmwareService firmwareService = new FirmwareService(sender);

        try
        {
            firmwareService.WaitForHearbeat();
            firmwareService.SetIsDataPaused(true);

            JsonDocument? networks = null;
            while (true){
                networks = firmwareService.ScanForWifiNetworks();
                if (networks is not null)
                    break;

                Console.WriteLine("Networks not found, retrying...");
            }

            Console.WriteLine("Networks:");
            Console.WriteLine(networks.RootElement.GetRawText());

            //var currentNetworks = FirmwareService.GetWifiStatus(port);
            //Console.WriteLine("Current Network:");
            //Console.WriteLine(currentNetworks);

            firmwareService.SetIsDataPaused(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending payload: {ex.Message}");
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static string GetSerialPort()
    {
        string[] availablePorts = SerialPort.GetPortNames();

        if (availablePorts.Length == 0)
        {
            Console.WriteLine("No serial ports found. Please connect your ESP32-S3.");
            Environment.Exit(1);
        }

        Console.WriteLine("Available serial ports:");
        for (int i = 0; i < availablePorts.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {availablePorts[i]}");
        }

        int selection;
        do
        {
            Console.Write("Select a port (enter number): ");
        } while (!int.TryParse(Console.ReadLine(), out selection) ||
                 selection < 1 ||
                 selection > availablePorts.Length);

        return availablePorts[selection - 1];
    }
}
