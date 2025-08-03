using System.Numerics;

public class GazeSample
{
    public DateTime Timestamp { get; set; }
    public Vector3 HeadPosition { get; set; }
    public Quaternion HeadRotation { get; set; }
    public Vector3 GazeOrigin { get; set; }
    public Vector3 GazeDirection { get; set; }
    public Vector3 TargetPosition { get; set; } // Sphere center
}

public class GazeCapture
{
    private readonly List<GazeSample> _samples = new();
    private readonly Func<Vector3> _getHeadPos;
    private readonly Func<Quaternion> _getHeadRot;
    private readonly Func<Vector3> _getSpherePos;

    public GazeCapture(Func<Vector3> getHeadPos, Func<Quaternion> getHeadRot, Func<Vector3> getSpherePos)
    {
        _getHeadPos = getHeadPos;
        _getHeadRot = getHeadRot;
        _getSpherePos = getSpherePos;
    }

    public void CaptureSample()
    {
        var headPos = _getHeadPos();
        var headRot = _getHeadRot();
        var spherePos = _getSpherePos();
        var gazeDir = Vector3.Normalize(spherePos - headPos);
        _samples.Add(new GazeSample
        {
            Timestamp = DateTime.UtcNow,
            HeadPosition = headPos,
            HeadRotation = headRot,
            GazeOrigin = headPos,
            GazeDirection = gazeDir,
            TargetPosition = spherePos
        });
    }

    public IReadOnlyList<GazeSample> GetSamples() => _samples;
    public void Clear() => _samples.Clear();
}
