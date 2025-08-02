using System.Numerics;

public class TrainingSample
{
    public DateTime Timestamp { get; set; }
    public byte[] LeftEyeImage { get; set; } = Array.Empty<byte>();
    public byte[] RightEyeImage { get; set; } = Array.Empty<byte>();
    public Vector3 HeadPosition { get; set; }
    public Quaternion HeadRotation { get; set; }
    public Vector3 GazeOrigin { get; set; }
    public Vector3 GazeDirection { get; set; }
    public Vector3 TargetPosition { get; set; }
}

public class TrainingDataWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private bool _disposed;

    public TrainingDataWriter(string filePath)
    {
        _writer = new BinaryWriter(File.Open(filePath, FileMode.Create));
        // Write header (magic, version, etc)
        _writer.Write("EYEGAZEDATA");
        _writer.Write(1); // version
    }

    public void WriteSample(TrainingSample sample)
    {
        _writer.Write(sample.Timestamp.ToBinary());
        _writer.Write(sample.LeftEyeImage.Length);
        _writer.Write(sample.LeftEyeImage);
        _writer.Write(sample.RightEyeImage.Length);
        _writer.Write(sample.RightEyeImage);
        WriteVector3(sample.HeadPosition);
        WriteQuaternion(sample.HeadRotation);
        WriteVector3(sample.GazeOrigin);
        WriteVector3(sample.GazeDirection);
        WriteVector3(sample.TargetPosition);
    }

    private void WriteVector3(Vector3 v)
    {
        _writer.Write(v.X); _writer.Write(v.Y); _writer.Write(v.Z);
    }
    private void WriteQuaternion(Quaternion q)
    {
        _writer.Write(q.X); _writer.Write(q.Y); _writer.Write(q.Z); _writer.Write(q.W);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _writer.Dispose();
            _disposed = true;
        }
    }
}
