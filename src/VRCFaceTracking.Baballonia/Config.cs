using Microsoft.Extensions.Hosting;

namespace VRCFaceTracking.Baballonia;

public class Config
{
	public string Host { get; }

    public int Port { get; }
    public bool IsEyeSupported { get; }

    public bool IsFaceSupported { get; }

    public Config(string Host, int Port, bool IsEyeSupported, bool IsFaceSupported)
    {
        this.Host = Host;
        this.Port = Port;
        this.IsEyeSupported = IsEyeSupported;
        this.IsFaceSupported = IsFaceSupported;
    }
}
