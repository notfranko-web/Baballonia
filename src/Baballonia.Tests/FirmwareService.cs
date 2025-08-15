using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text.Json;
using static Baballonia.Tests.FirmwareRequests;

namespace Baballonia.Tests
{
    public class FirmwareService
    {
        private ICommandSenderFactory _commandSenderFactory;
        private ILogger<FirmwareService> _logger;

        public FirmwareService(ILogger<FirmwareService> logger, ICommandSenderFactory commandSenderFactory)
        {
            _logger = logger;
            _commandSenderFactory = commandSenderFactory;
        }

        private string[] FindAvalibleComPorts()
        {
            // GetPortNames() may return single port multiple times
            // https://stackoverflow.com/questions/33401217/serialport-getportnames-returns-same-port-multiple-times
            return SerialPort.GetPortNames().Distinct().ToArray();
        }

        public FirmwareSession StartSession(CommandSenderType type, string port)
        {
            return new FirmwareSession(_commandSenderFactory.Create(type, port), _logger);
        }

        public string[] ProbeComPorts(TimeSpan timeout)
        {
            var ports = FindAvalibleComPorts();
            List<string> goodPorts = [];
            foreach (var port in ports)
            {
                var session = StartSession(CommandSenderType.Serial, port);
                try
                {
                    _logger.LogInformation("Probing {}", port);
                    var heartbeat = session.WaitForHeartbeat(timeout);
                    if (heartbeat != null)
                    {
                        goodPorts.Add(port);
                    }

                    session.Dispose();
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
                    session.Dispose();
                }
            }

            return [.. goodPorts];
        }
    }
}
