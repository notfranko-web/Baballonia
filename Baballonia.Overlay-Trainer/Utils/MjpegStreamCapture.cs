using OpenCvSharp;

public class MjpegStreamCapture : IDisposable
{
    private readonly string _url;
    private readonly Action<Mat> _onFrame;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public MjpegStreamCapture(string url, Action<Mat> onFrame)
    {
        _url = url;
        _onFrame = onFrame;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _captureTask?.Wait();
    }

    private async Task CaptureLoop(CancellationToken token)
    {
        using var http = new HttpClient();
        using var stream = await http.GetStreamAsync(_url);
        var mjpeg = new MjpegDecoder(stream);
        while (!token.IsCancellationRequested)
        {
            var frame = mjpeg.ReadFrame();
            if (frame != null)
                _onFrame(frame);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}

// Dummy MJPEG decoder for structure; replace with actual OpenCVSharp decoding implementation
public class MjpegDecoder
{
    private Stream _stream;
    public MjpegDecoder(Stream stream) { _stream = stream; }
    public Mat? ReadFrame() { return null; } // TODO: Implement MJPEG decoding to Mat
}
