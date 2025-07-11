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

        public void SetIsDataPaused(bool isPaused)
        {
            var payload = Commands.SetDataPaused(isPaused);
            _logger.LogDebug("Sending payload: {}", payload);
            _commandSender.WriteLine(payload);

            var resstr = _commandSender.ReadLine();
            var jsons = FindJsonObjects(resstr);
            jsons.ForEach(j => _logger.LogDebug("Recieved json: {}", j.RootElement.GetRawText()));
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
        public JsonDocument? ScanForWifiNetworks()
        {
            var payload = Commands.ScanWifiNetworks();
            _logger.LogDebug("Sending payload: {}", payload);
            _commandSender.WriteLine(payload);
            while (true)
            {
                Thread.Sleep(10); // give it some breathing time

                var resstr = _commandSender.ReadLine();
                var jsons = FindJsonObjects(resstr);
                jsons.ForEach(j => _logger.LogDebug("Recieved json: {}", j.RootElement.GetRawText()));
                if (jsons.Count > 0)
                {
                    var networksJson = FindJsonWithKeyPrefix(jsons, "networks");
                    if (networksJson != null)
                        return networksJson;
                }
            }
        }

        private JsonDocument? FindJsonWithKeyPrefix(List<JsonDocument> docs, string keyToFind)
        {
            foreach (var doc in docs)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.Equals(keyToFind, StringComparison.OrdinalIgnoreCase))
                        return doc;
                }
            }

            return null;
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

        private string[] FindAvalibleComPorts()
        {
            return SerialPort.GetPortNames();
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

        public void Dispose()
        {
            if (_commandSender != null)
                _commandSender.Dispose();
        }
    }
}
