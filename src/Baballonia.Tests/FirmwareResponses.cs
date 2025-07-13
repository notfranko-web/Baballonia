using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Baballonia.Tests
{
    public class FirmwareResponses
    {
        public interface Args { }
        public class WifiNetwork
        {
            [JsonPropertyName("ssid")]
            public required string Ssid { get; set; }

            [JsonPropertyName("channel")]
            public int Channel { get; set; }

            [JsonPropertyName("rssi")]
            public int Rssi { get; set; }

            [JsonPropertyName("mac_address")]
            public required string MacAddress { get; set; }

            [JsonPropertyName("auth_mode")]
            public int AuthMode { get; set; }
        }
        public class WifiNetworkArgs : Args
        {
            [JsonPropertyName("networks")]
            public required List<WifiNetwork> Networks;
        }

        public class WifiStatusArgs : Args
        {
            [JsonPropertyName("status")]
            public required string Status { get; set; }
            [JsonPropertyName("networks_configured")]
            public int NetworksConfigured { get; set; }
            [JsonPropertyName("ip_address")]
            public required string IpAddress { get; set; }
        }

        public class Response<T> where T : Args
        {
            public string CommandName { get; }
            public string Status { get; }
            public T Args { get; }

            public Response(string commandName, string status, T args)
            {
                CommandName = commandName;
                Status = status;
                Args = args;
            }
        }

        public class GenericResponse
        {
            [JsonPropertyName("command_name")]
            public required string CommandName { get; set; }
            [JsonPropertyName("status")]
            public required string Status { get; set; }

            [JsonPropertyName("args")]
            public JsonDocument? Args { get; }

            public Response<ArgType> CastResponseType<ArgType>() where ArgType : Args
            {
                if (Args == null) 
                    throw new ArgumentNullException("Trying to deserialize response without arguments");

                var a = Args.Deserialize<ArgType>();
                return new Response<ArgType>(CommandName, Status, a);
            }
        }
        public class CommandResponse
        {
            [JsonPropertyName("results")]
            public required List<GenericResponse> Results { get; set; }
        }
    }
}
