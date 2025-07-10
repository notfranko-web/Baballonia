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
        string port = SerialPort.GetPortNames().FirstOrDefault()!;
        // const int baudRate = 921600;

        try
        {
            // Send the payload over serial
            FirmwareService.SetIsDataPaused(port, true);

            bool loop = true;
            var networks = FirmwareService.ScanForWifiNetworks(port);
            do
            {
                networks = FirmwareService.ScanForWifiNetworks(port);
                if (networks is not null)
                {
                    loop = false;
                }
            } while (loop);

            Console.WriteLine("Networks:");
            Console.WriteLine(networks);

            var currentNetworks = FirmwareService.GetWifiStatus(port);
            Console.WriteLine("Current Network:");
            Console.WriteLine(currentNetworks);

            FirmwareService.SetIsDataPaused(port, false);
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
