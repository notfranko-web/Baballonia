using Newtonsoft.Json;

namespace Baballonia.Desktop.Calibration.Aero.Overlay;

public class ApiResponse
{
    [JsonProperty("result")]
    public string Result { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }
}
