using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public class FirmwareService
    {
        private SerialCommandSender serialCommandSender;

        public FirmwareService(SerialCommandSender serialCommandSender)
        {
            this.serialCommandSender = serialCommandSender;
        }

        public void SetIsDataPaused(bool isPaused)
        {
            var payload = Commands.SetDataPaused(isPaused);
            serialCommandSender.WriteCommand(payload);

            var resstr = serialCommandSender.ReadResponse();
            var jsons = FindJsonObjects(resstr);
            jsons.ForEach((j) => Console.WriteLine("Recieved json: " + j.RootElement.GetRawText()));
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

                    if(braceDepth == 0 && jsonStart != -1)
                    {
                        string potentialJson = input.Substring(jsonStart, i - jsonStart + 1);
                        try
                        {
                            var json = JsonDocument.Parse(potentialJson);
                            jsonObjects.Add(json);
                        }
                        catch(JsonException)
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
            serialCommandSender.WriteCommand(payload);
            Thread.Sleep(10000); // There should be a better way
            var resstr = serialCommandSender.ReadResponse();
            var jsons = FindJsonObjects(resstr);
            jsons.ForEach((j) => Console.WriteLine("Recieved json: " + j.RootElement.GetRawText()));
            if (jsons.Count > 0)
            {
                var networksJson = FindJsonWithKeyPrefix(jsons, "networks");
                if (networksJson != null)
                    return networksJson;
            }    

            return null;
        }

        private JsonDocument? FindJsonWithKeyPrefix(List<JsonDocument> docs, string keyToFind)
        {
            foreach (var doc in docs)
            {
                if(doc.RootElement.ValueKind != JsonValueKind.Object) continue;

                foreach(var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.Equals(keyToFind, StringComparison.OrdinalIgnoreCase))
                        return doc;
                }
            }

            return null;
        }


        public JsonDocument WaitForHearbeat()
        {
            while (true)
            {
                var resstr = serialCommandSender.ReadResponse();
                var jsons = FindJsonObjects(resstr);
                jsons.ForEach((j) => Console.WriteLine("Recieved json: " + j.RootElement.GetRawText()));
                if (jsons.Count > 0)
                {
                    var heartbeatJson = FindJsonWithKeyPrefix(jsons, "heartbeat");
                    if (heartbeatJson != null)
                        return heartbeatJson;
                }    
            }
        }

    }
}
