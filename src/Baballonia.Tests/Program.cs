using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace Baballonia.Tests;

public class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("ESP32-S3 Wi-Fi Tool");
        Console.WriteLine("-------------------------");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLogging((loggingBuilder) => loggingBuilder
            .AddConsole()
            .AddDebug()
            .SetMinimumLevel(LogLevel.Debug));


        services.AddSingleton<CommandSenderFactory>();
        services.AddTransient<FirmwareService>();

        var serviceProvider = services.BuildServiceProvider();
        var _logger = serviceProvider.GetRequiredService<ILogger<Program>>()!;

        FirmwareService firmwareService = serviceProvider.GetService<FirmwareService>()!;

        try
        {
            string[] goodPorts = firmwareService.ProbeComPorts(TimeSpan.FromSeconds(10));
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
                _logger.LogInformation("Multiple suitable devices were found: {}", (object)goodPorts); // https://stackoverflow.com/questions/66317482/serilog-only-logs-first-element-of-array
                var res = AskUser("Multiple suitable devices were found", goodPorts);
                selectedPort = res.selectedOption;
            }


            firmwareService.StartSession(selectedPort);
            firmwareService.WaitForHeartbeat();
            firmwareService.SetIsDataPaused(true);

            JsonDocument? networks = null;
            while (true)
            {
                networks = firmwareService.ScanForWifiNetworks();
                if (networks is not null)
                    break;

                _logger.LogWarning("Networks not found, retrying...");
            }

            _logger.LogInformation("Found networks: {}", networks.RootElement.GetRawText());

            //var currentNetworks = FirmwareService.GetWifiStatus(port);
            //Console.WriteLine("Current Network:");
            //Console.WriteLine(currentNetworks);

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

}
