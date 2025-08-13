using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Baballonia.Tests.FirmwareTests
{
    class MockCommandSender : ICommandSender
    {
        private List<string> lines;

        public MockCommandSender(List<string> lines)
        {
            this.lines = lines;
        }

        public void Dispose()
        {

        }

        public string ReadLine()
        {
            string line = lines[0];
            lines.RemoveAt(0);

            return line;
        }

        public void WriteLine(string message)
        {
        }
    }
    [TestClass]
    public class FirmwareServiceTest
    {
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;

        [TestInitialize]
        public void Initialize()
        {
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Trace);
            });

            _logger = _loggerFactory.CreateLogger("TEST");
        }


        private FirmwareService CreateService(List<string> mockResponses)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Trace);
            });

            var serviceLogger = loggerFactory.CreateLogger<FirmwareService>();
            var logger = loggerFactory.CreateLogger<FirmwareServiceTest>();

            var mockCommandSenderFactory = new Mock<ICommandSenderFactory>();
            mockCommandSenderFactory
                .Setup(s => s.Create(CommandSenderType.Serial, "COM4"))
                .Returns(new MockCommandSender(mockResponses));

            return new FirmwareService(serviceLogger, mockCommandSenderFactory.Object);
        }
        [TestMethod]
        public void TestSendCommand()
        {
            List<string> mockLines = new List<string>();
            mockLines.Add("""{"heartbeat":{}}""");
            mockLines.Add("""{"results":        ["{\"result\":\"{\\\"status\\\":\\\"connected\\\",\\\"networks_configured\\\":1,\\\"ip_address\\\":\\\"192.168.0.246\\\"}\"}"]} """);

            var firmwareService = CreateService(mockLines);

            firmwareService.StartSession(CommandSenderType.Serial, "COM4");
            firmwareService.WaitForHeartbeat();

            var builder = FirmwareCommands.Builder();
            var command = builder.GetWifiStatus().build();
            var results = firmwareService.SendCommand(command);
            var r1 = results.CastResponseType<FirmwareResponses.WifiStatusArgs>();

        }


        [TestMethod]
        public void TestSendChainCommand()
        {
            List<string> mockLines = new List<string>();
            mockLines.Add("""{"heartbeat":{}}""");
            mockLines.Add("""
                    {"results":[
                    {"command_name":"pause", "status": "SUCCESS" },
                    {"command_name":"get_wifi_status", "status": "SUCCESS" }
                ]}
                """);

            var firmwareService = CreateService(mockLines);
            firmwareService.StartSession(CommandSenderType.Serial, "COM4");
            firmwareService.WaitForHeartbeat();

            var builder = FirmwareCommands.Builder();
            var command = builder.GetWifiStatus().build();
            var commandResult = firmwareService.SendCommand(command);

        }

        [TestMethod]
        public void TestTui()
        {
            string fakeInput =
                "1" + Environment.NewLine +
                "8" + Environment.NewLine +
                "8" + Environment.NewLine +
                "a" + Environment.NewLine;
            Console.SetIn(new StringReader(fakeInput));
            Program.Main1([]);
        }

    }
}
