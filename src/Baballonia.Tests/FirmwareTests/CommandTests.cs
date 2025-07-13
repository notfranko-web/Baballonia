using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Baballonia.Tests.FirmwareTests
{
    [TestClass]
    public class CommandTests
    {
        [TestMethod]
        public void CommandBuilderTestPause()
        {
            var builder = new FirmwareCommands.CommandBuilder();
            var cmdJsonStr = builder.SetDataPaused(true).build().serialize();

            string testJson = """{"commands": [{"command": "pause", "data":{"pause": true}}]}""";

            var j1 = JToken.Parse(cmdJsonStr);
            var j2 = JToken.Parse(testJson);

            string errorMessage = string.Format("JSON strings are not equal: \n{0} \n{1}", cmdJsonStr, testJson);
            Assert.IsTrue(JToken.DeepEquals(j1, j2), errorMessage);
        }
        [TestMethod]
        public void CommandBuilderTestMode()
        {
            var builder = new FirmwareCommands.CommandBuilder();
            var cmdJsonStr = builder.SetStreamMode(FirmwareCommands.Mode.Wifi).build().serialize();

            string testJson = """{"commands": [{"command": "switch_mode", "data":{"mode": "wifi"}}]}""";

            var j1 = JToken.Parse(cmdJsonStr);
            var j2 = JToken.Parse(testJson);

            string errorMessage = string.Format("JSON strings are not equal: \n{0} \n{1}", cmdJsonStr, testJson);
            Assert.IsTrue(JToken.DeepEquals(j1, j2), errorMessage);
        }
        [TestMethod]
        public void CommandBuilderTestChaining()
        {
            var builder = new FirmwareCommands.CommandBuilder();
            var cmdJsonStr = builder.SetDataPaused(true).SetStreamMode(FirmwareCommands.Mode.UVC).build().serialize();

            string testJson = """
                {"commands": [
                    {"command": "pause", "data":{"pause": true}},
                    {"command": "switch_mode", "data":{"mode": "uvc"}}
                ]}
                """;

            var j1 = JToken.Parse(cmdJsonStr);
            var j2 = JToken.Parse(testJson);

            string errorMessage = string.Format("JSON strings are not equal: \n{0} \n{1}", cmdJsonStr, testJson);
            Assert.IsTrue(JToken.DeepEquals(j1, j2), errorMessage);
        }
    }
}
