using System.Text.RegularExpressions;
using OpenCvSharp;

namespace Baballonia.SDK;

/// <summary>
/// Defines custom camera stream behavior
/// </summary>
public abstract class Capture
{
    /// <summary>
    /// What unique strings are used to open this device?
    /// </summary>
    public abstract HashSet<Regex> Connections { get; set; }

    /// <summary>
    /// Where this Capture source is currently pulling data from
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Dimensions for this frame. Needs to be explicitly defined when dealing with
    /// custom capture sources (IE Serial/Cameras)
    /// </summary>
    public (int width, int height) Dimensions;

    /// <summary>
    /// Represents the incoming frame data for this capture source.
    /// Will be `dimension` in BGR color space
    /// </summary>
    public Mat RawMat { get; protected set; } = new();

    /// <summary>
    /// Is this Capture source ready to produce data?
    /// </summary>
    public bool IsReady { get; protected set; }

    /// <summary>
    /// Start Capture on this source
    /// </summary>
    /// <returns></returns>
    public abstract Task<bool> StartCapture();

    /// <summary>
    /// Stop Capture on this source
    /// </summary>
    /// <returns></returns>
    public abstract Task<bool> StopCapture();

    protected Capture(string url)
    {
        this.Url = url;
        IsReady = false;
    }
}
