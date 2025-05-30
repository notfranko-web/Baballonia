namespace VRCFaceTracking.Babble;

public class Config
{
    public (bool eyeSuccess, bool expressionSuccess) EnabledFeatrures { get; set; } = (true, true);
	public string Host { get; set; } = null!;

    public int Port { get; set; }
}
