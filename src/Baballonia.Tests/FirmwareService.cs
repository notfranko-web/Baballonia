using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace Baballonia.Tests;

public static class FirmwareService
{
    public static event Action OnCommandStart;
    public static event Action<string> OnFirmwareUpdate;
    public static event Action OnCommandEnd;
    public static event Action<string> OnCommandError;

    private static readonly string EsptoolCommand;
    private const int DefaultBaudRate = 921600; // esptool-rs: Setting baud rate higher than 115,200 can cause issues

    static FirmwareService()
    {
        EsptoolCommand = OperatingSystem.IsWindows() ? "espflash.exe" : "espflash";
    }

    /// <summary>
    /// Uploads firmware to an ESP32-S3 device using a subprocess esptool-rs
    /// </summary>
    /// <param name="port">COM port where the device is connected</param>
    /// <param name="pathToFirmware">Path to the firmware file to upload</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static void UploadFirmware(string port, string pathToFirmware)
    {
        try
        {
            // Check if firmware file exists
            if (!File.Exists(pathToFirmware))
            {
                OnCommandError($"Firmware file not found: {pathToFirmware}");
                return;
            }

            // Notify start of firmware update
            OnCommandStart();

            // Create process to run esptool.py
            if (!RunEspSubprocess(
                    arguments:
                    $"write-bin 0x00 \"{pathToFirmware}\" --port {port} --baud {DefaultBaudRate}"))
            {
                OnCommandError($"Firmware update failed!");
            }

            // Wired firmware update completed successfully
            OnCommandEnd();
        }
        catch (Exception ex)
        {
            OnCommandError?.Invoke($"Firmware update failed: {ex.Message}");
        }
    }

    // 0
    public static void SetIsDataPaused(string port, bool isPaused)
    {
        string payload = $@"{{""commands"": [{{""command"": ""pause"", ""data"": {{""pause"": {isPaused.ToString().ToLower()}}}}}]}}";
        SendSerialCommand(port, payload);
    }

    // 1
    public static JsonDocument ScanForWifiNetworks(string port)
    {
        string payload = $@"{{""commands"": [{{""command"": ""scan_networks""}}]}}";
        return SendSerialCommandWithJsonResponse(port, payload);
    }

    // Skip #2 Show available networks

    // 3


    // 4
    public static Connectivity GetWifiStatus(string port)
    {
        string payload = $@"{{""commands"": [{{""command"": ""get_wifi_status""}}]}}}}}}]}}";

        var response = SendSerialCommandWithJsonResponse(port, payload);
        var rootNode = response!.RootElement;

        if (rootNode.TryGetProperty("results", out JsonElement resultsArray))
        {
            var firstResult = resultsArray[0];

            string nestedJsonString = firstResult.GetString()!;
            using var nestedDoc = JsonDocument.Parse(nestedJsonString);

            if (nestedDoc.RootElement.TryGetProperty("result", out JsonElement resultProperty))
            {
                string innerJsonString = resultProperty.GetString()!;

                // Parse the innermost JSON
                using var innerDoc = JsonDocument.Parse(innerJsonString);

                // Finally get the "status" value
                if (innerDoc.RootElement.TryGetProperty("status", out JsonElement statusElement))
                {
                    if (Enum.TryParse(statusElement.GetString(), out Connectivity status))
                    {
                        return status;
                    }
                }
            }
        }

        return Connectivity.not_connected;
    }

    private static JsonDocument SendSerialCommandWithJsonResponse(string port, string payload)
    {
        StringBuilder responseBuilder = new StringBuilder();

        try
        {
            // Create a new SerialPort object with the specified port name and baud rate
            using SerialPort serialPort = new SerialPort(port, DefaultBaudRate);

            // Set serial port parameters
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Parity = Parity.None;
            serialPort.Handshake = Handshake.None;

            // Set read/write timeouts
            serialPort.ReadTimeout = 5000;
            serialPort.WriteTimeout = 5000;
            serialPort.Encoding = Encoding.UTF8;

            try
            {
                // Open the port
                serialPort.Open();
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                // Convert the payload to bytes
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

                // Write the payload to the serial port
                const int chunkSize = 64;
                for (int i = 0; i < payloadBytes.Length; i += chunkSize)
                {
                    int length = Math.Min(chunkSize, payloadBytes.Length - i);
                    serialPort.Write(payloadBytes, i, length);
                    Thread.Sleep(50); // Small pause between chunks
                }

                // Add a newline to indicate end of message
                serialPort.Write("\n");

                // Wait
                Thread.Sleep(1000);

                // Read the response
                DateTime startTime = DateTime.Now;
                const int maxWaitTime = 10000; // 10 seconds maximum wait time

                do
                {
                    // Check for timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > maxWaitTime)
                    {
                        OnCommandError("Timeout waiting for serial response");
                        return null;
                    }

                    // Read available data
                    if (serialPort.BytesToRead > 0)
                    {
                        string receivedData = serialPort.ReadLine();
                        responseBuilder.Append(receivedData);
                    }
                    else
                    {
                        // Small delay to prevent CPU spinning
                        Thread.Sleep(10);
                    }
                } while (serialPort.BytesToRead > 0 || responseBuilder.Length == 0);

                return JsonDocument.Parse(responseBuilder.ToString().Trim());;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                // Close the port
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
        }
        catch (Exception ex)
        {
            OnCommandError($"Firmware update failed: {ex.Message}");
        }

        return null;
    }

    private static string SendSerialCommandWithStringResponse(string port, string payload)
    {
        string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        try
        {
            // Create a new SerialPort object with the specified port name and baud rate
            using SerialPort serialPort = new SerialPort(port, DefaultBaudRate);

            // Set serial port parameters
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Parity = Parity.None;
            serialPort.Handshake = Handshake.None;

            // Set read/write timeouts
            serialPort.ReadTimeout = 5000;
            serialPort.WriteTimeout = 5000;

            try
            {
                // Open the port
                serialPort.Open();
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                // Convert the payload to bytes
                byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

                // Write the payload to the serial port
                const int chunkSize = 64;
                for (int i = 0; i < payloadBytes.Length; i += chunkSize)
                {
                    int length = Math.Min(chunkSize, payloadBytes.Length - i);
                    serialPort.Write(payloadBytes, i, length);
                    Thread.Sleep(50); // Small pause between chunks
                }

                // Add a newline to indicate end of message
                serialPort.Write("\n");

                // Wait
                Thread.Sleep(1000);

                // Read the response
                StringBuilder responseBuilder = new StringBuilder();
                DateTime startTime = DateTime.Now;
                const int maxWaitTime = 10000; // 10 seconds maximum wait time

                do
                {
                    // Check for timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > maxWaitTime)
                    {
                        OnCommandError("Timeout waiting for serial response");
                        return string.Empty;
                    }

                    // Read available data
                    if (serialPort.BytesToRead > 0)
                    {
                        string receivedData = serialPort.ReadLine();
                        responseBuilder.Append(receivedData);

                        // Check if we received a complete message (ends with newline)
                        if (receivedData.Contains('\n'))
                        {
                            break;
                        }
                    }
                    else
                    {
                        // Small delay to prevent CPU spinning
                        Thread.Sleep(10);
                    }
                }
                while (serialPort.BytesToRead > 0 || responseBuilder.Length == 0);

                return responseBuilder.ToString().Trim();
            }
            finally
            {
                // Close the port
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
        }
        catch (Exception ex)
        {
            OnCommandError($"Firmware update failed: {ex.Message}");
        }

        return string.Empty;
    }

    private static void SendSerialCommand(string port, string payload)
    {
        string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        try
        {
            // Create a new SerialPort object with the specified port name and baud rate
            using SerialPort serialPort = new SerialPort(port, DefaultBaudRate);

            // Set serial port parameters
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Parity = Parity.None;
            serialPort.Handshake = Handshake.None;

            // Set read/write timeouts
            serialPort.ReadTimeout = 5000;
            serialPort.WriteTimeout = 5000;

            try
            {
                // Open the port
                serialPort.Open();
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                // Convert the payload to bytes
                byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

                // Write the payload to the serial port
                const int chunkSize = 64;
                for (int i = 0; i < payloadBytes.Length; i += chunkSize)
                {
                    int length = Math.Min(chunkSize, payloadBytes.Length - i);
                    serialPort.Write(payloadBytes, i, length);
                    Thread.Sleep(50); // Small pause between chunks
                }

                // Add a newline to indicate end of message
                serialPort.Write("\n");

                // Wait
                Thread.Sleep(1000);
            }
            finally
            {
                // Close the port
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
        }
        catch (Exception ex)
        {
            OnCommandError($"Firmware update failed: {ex.Message}");
        }
    }

    private static bool RunEspSubprocess(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = EsptoolCommand;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            OnCommandError($"Firmware update failed: {ex.Message}");
            return false;
        }
    }
}
