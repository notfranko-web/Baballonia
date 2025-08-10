using System;
using Avalonia;
using Baballonia.Services.Inference.Enums;
using Baballonia.Services.Inference.Models;
using SkiaSharp;

namespace Baballonia.Helpers;

public class CameraSize()
{
    public int Width = 0;
    public int Height = 0;
}
public class CropManager()
{
    private bool _isCropping;
    private double _startX;
    private double _startY;
    private CameraSettings.RegionOfInterest _cropZone;

    public CameraSettings.RegionOfInterest CropZone => _cropZone;
    public bool IsCropping => _isCropping;
    public CameraSize CameraSize { get; set; } = new();

    public void StartCrop(Point startPosition)
    {
        _startX = startPosition.X;
        _startY = startPosition.Y;
        _isCropping = true;
    }

    public void UpdateCrop(Point currentPosition)
    {
        if (!_isCropping) return;

        // Clamp currentPosition to the image boundaries
        double clampedX = Math.Max(0, Math.Min(currentPosition.X, CameraSize.Width));
        double clampedY = Math.Max(0, Math.Min(currentPosition.Y, CameraSize.Height));

        double x, y, w, h;

        if (clampedX < _startX)
        {
            x = clampedX;
            w = _startX - x;
        }
        else
        {
            x = _startX;
            w = clampedX - _startX;
        }

        if (clampedY < _startY)
        {
            y = clampedY;
            h = _startY - y;
        }
        else
        {
            y = _startY;
            h = clampedY - _startY;
        }

        _cropZone = new CameraSettings.RegionOfInterest(
            (int)x,
            (int)y,
            (int)w,
            (int)h
        );
    }

    public void EndCrop()
    {
        _isCropping = false;
    }

    public void SetCropZone(CameraSettings.RegionOfInterest rectangle)
    {
        _cropZone = rectangle;
    }

    public void SelectEntireFrame(Camera camera)
    {
        // Special BSB2E-like stereo camera logic
        var halfWidth = CameraSize.Width / 2;

        int x;
        int y;
        int width;
        int height;

        if (CameraSize.Width / 2 == CameraSize.Width)
        {
            switch (camera)
            {
                case Camera.Left:
                    x = 0;
                    y = 0;
                    width = halfWidth - 1;
                    height = CameraSize.Height - 1;
                    break;
                case Camera.Right:
                    x = halfWidth;
                    y = 0;
                    width = halfWidth - 1;
                    height = CameraSize.Height - 1;
                    break;
                default: // Face
                    x = 0;
                    y = 0;
                    width = CameraSize.Width;
                    height = CameraSize.Width;
                    break;
            }
        }
        else
        {
            x = 0;
            y = 0;
            width = CameraSize.Width;
            height = CameraSize.Height;
        }

        _cropZone = new CameraSettings.RegionOfInterest(x, y, width, height);
    }
}
