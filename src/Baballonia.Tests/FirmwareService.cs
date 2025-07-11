using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public class FirmwareService : IDisposable
    {
        private CommandSenderFactory _commandSenderFactory;
        private ILogger<FirmwareService> _logger;
        private ICommandSender? _commandSender = null;

        private bool JsonHasPrefix(JsonDocument json, string key)
        {
            if (json.RootElement.ValueKind != JsonValueKind.Object) return false;

            foreach (var prop in json.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private JsonDocument? FindJsonWithKeyPrefix(List<JsonDocument> docs, string keyToFind)
        {
            foreach (var doc in docs)
            {
                if (JsonHasPrefix(doc, keyToFind))
                    return doc;
            }

            return null;
        }

        private string[] FindAvalibleComPorts()
        {
            return SerialPort.GetPortNames().Distinct().ToArray();  // https://stackoverflow.com/questions/33401217/serialport-getportnames-returns-same-port-multiple-times
        }
        private List<JsonDocument> FindJsonObjects(string input)
        {
            var jsonObjects = new List<JsonDocument>();
            int braceDepth = 0;
            int jsonStart = 1;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '{')
                {
                    if (braceDepth == 0)
                        jsonStart = i;
                    braceDepth++;
                }
                else if (input[i] == '}')
                {
                    braceDepth--;

                    if (braceDepth == 0 && jsonStart != -1)
                    {
                        string potentialJson = input.Substring(jsonStart, i - jsonStart + 1);
                        try
                        {
                            var json = JsonDocument.Parse(potentialJson);
                            jsonObjects.Add(json);
                        }
                        catch (JsonException)
                        {
                            //ignore
                        }
                        jsonStart = -1;
                    }
                }
            }
            return jsonObjects;

        }

        public FirmwareService(ILogger<FirmwareService> loger, CommandSenderFactory commandSenderFactory)
        {
            this._logger = loger;
            this._commandSenderFactory = commandSenderFactory;
        }

        public void StartSession(string port)
        {
            _commandSender = _commandSenderFactory.Create(port);
        }

        public void StopSession()
        {
            if (_commandSender != null)
            {
                _commandSender.Dispose();
                _commandSender = null;
            }
        }
        public JsonDocument? WaitForHeartbeat()
        {
            return WaitForHeartbeat(TimeSpan.FromSeconds(10));
        }

        public JsonDocument? WaitForHeartbeat(TimeSpan timeout)
        {
            var startTime = DateTime.Now;
            while (true)
            {
                if (DateTime.Now - startTime > timeout)
                    throw new TimeoutException("Timeout reached");

                var resstr = _commandSender.ReadLine();
                var jsons = FindJsonObjects(resstr);
                jsons.ForEach((j) => _logger.LogDebug("Recieved json: {}", j.RootElement.GetRawText()));
                if (jsons.Count > 0)
                {
                    var heartbeatJson = FindJsonWithKeyPrefix(jsons, "heartbeat");
                    if (heartbeatJson != null)
                        return heartbeatJson;
                }
            }
        }

        public string[] ProbeComPorts(TimeSpan timeout)
        {
            var ports = FindAvalibleComPorts();
            List<string> goodPorts = [];
            foreach (var port in ports)
            {
                try
                {
                    StartSession(port);
                    _logger.LogInformation("Probing {}", port);
                    var heartbeat = WaitForHeartbeat(timeout);
                    if (heartbeat != null)
                    {
                        goodPorts.Add(port);
                    }
                }
                catch (TimeoutException ex)
                {
                    _logger.LogInformation("probing port {}: timeout reached", port);

                }
                catch (Exception ex)
                {
                    _logger.LogError("Error probing port {}: {}", port, ex.Message);
                }
                finally
                {
                    StopSession();
                }
            }

            return [.. goodPorts];
        }

        public JsonDocument? SetIsDataPaused(bool isPaused)
        {
            return SendCommandReadResponse(Commands.SetDataPaused(isPaused), "results");
        }

        public JsonDocument? ScanForWifiNetworks()
        {
            return SendCommandReadResponse(Commands.ScanWifiNetworks(), "networks");
        }

        public JsonDocument? SetWifi(string ssid, string password)
        {
            return SendCommandReadResponse(Commands.SetWifi(ssid, password), "results");
        }
        public JsonDocument? GetWifiStatus()
        {
            return SendCommandReadResponse(Commands.GetWifiStatus(), "results");
        }
        public JsonDocument? ConnectWifi()
        {
            return SendCommandReadResponse(Commands.ConnectWifi(), "results");
        }
        public JsonDocument? StartStreaming()
        {
            return SendCommandReadResponse(Commands.StartStreaming(), "results");
        }
        public JsonDocument? GetDeviceMode()
        {
            return SendCommandReadResponse(Commands.GetDeviceMode(), "results");
        }
        public JsonDocument? SetDeviceMode(Commands.Mode mode)
        {
            return SendCommandReadResponse(Commands.SwitchMode(mode), "results");
        }

        private JsonDocument? SendCommandReadResponse(string command, string responseJsonRootKey)
        {
            var payload = command;
            _logger.LogDebug("Sending payload: {}", payload);
            _commandSender.WriteLine(payload);
            JsonExtractor jsonExtractor = new JsonExtractor();
            while (true)
            {
                Thread.Sleep(10); // give it some breathing time

                JsonDocument json = jsonExtractor.ReadUntilValidJson(() => _commandSender.ReadLine());
                _logger.LogDebug("Recieved json: {}", json.RootElement.GetRawText());
                if (JsonHasPrefix(json, responseJsonRootKey))
                    return json;

            }
        }



        public void Dispose()
        {
            if (_commandSender != null)
                _commandSender.Dispose();
        }
    }
}
