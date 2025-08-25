using System;
using Baballonia.Services.Inference.Enums;
using OpenCvSharp;

namespace Baballonia.Services.Inference;

public class DualCameraSource : IVideoSource
{
    public IVideoSource? LeftCam;
    public IVideoSource? RightCam;

    private Mat? LastLeftImage;
    private Mat? LastRightImage;

    public bool Start()
    {
        return (LeftCam?.Stop() ?? true) && (RightCam?.Stop() ?? true);
    }

    public bool Stop()
    {
        return (LeftCam?.Start() ?? true) && (RightCam?.Start() ?? true);
    }

    // Here we try to acquire 2 images from both cameras and stitch them into a single image
    // if at least one image can be acquired, try to stitch it with last second image
    public Mat? GetFrame(ColorType? color = null)
    {
        var leftImage = LeftCam?.GetFrame(color);
        var rightImage = RightCam?.GetFrame(color);

        if (leftImage == null && rightImage == null)
            return null;

        // Track which images are new vs fallback
        var leftIsNew = leftImage != null;
        var rightIsNew = rightImage != null;

        leftImage ??= LastLeftImage;
        rightImage ??= LastRightImage;

        switch (leftImage)
        {
            case null when rightImage == null:
                return null;
            case null:
                leftImage = rightImage.Clone();
                break;
            default:
                // Don't clone - preserve both cameras when both are available!
                if (rightImage == null)
                    rightImage = leftImage.Clone();
                break;
        }

        int height = Math.Max(leftImage.Rows, rightImage.Rows);
        int width = leftImage.Cols + rightImage.Cols;

        Mat result = new Mat(height, width, leftImage.Type(), Scalar.All(0));

        leftImage.CopyTo(result[new Rect(0, 0, leftImage.Cols, leftImage.Rows)]);
        rightImage.CopyTo(result[new Rect(leftImage.Cols, 0, rightImage.Cols, rightImage.Rows)]);

        // Update last images only for new frames
        if (leftIsNew)
        {
            LastLeftImage?.Dispose();
            LastLeftImage = leftImage.Clone();
        }
        
        if (rightIsNew)
        {
            LastRightImage?.Dispose();
            LastRightImage = rightImage.Clone();
        }

        // Only dispose new images, not the cached LastImages
        if (leftIsNew)
            leftImage.Dispose();
        if (rightIsNew)
            rightImage.Dispose();

        return result;
    }

    public void Dispose()
    {
        LeftCam?.Dispose();
        RightCam?.Dispose();
        LastLeftImage?.Dispose();
        LastRightImage?.Dispose();
    }
}
