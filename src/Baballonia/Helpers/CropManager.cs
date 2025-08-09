using System;
using Avalonia;
using Baballonia.Services.Inference.Enums;
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
    private Rect _cropZone;

    public Rect CropZone => _cropZone;
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

        double x, y, w, h;

        if (currentPosition.X < _startX)
        {
            x = currentPosition.X;
            w = _startX - x;
        }
        else
        {
            x = _startX;
            w = currentPosition.X - _startX;
        }

        if (currentPosition.Y < _startY)
        {
            y = currentPosition.Y;
            h = _startY - y;
        }
        else
        {
            y = _startY;
            h = currentPosition.Y - _startY;
        }

        // Limit size to image bounds
        _cropZone = new Rect(
            x,
            y,
            Math.Min(CameraSize.Width, w),
            Math.Min(CameraSize.Height, h)
        );
    }

    public void EndCrop()
    {
        _isCropping = false;
    }

    public void SetCropZone(Rect rectangle)
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

        _cropZone = new Rect(x, y, width, height);
    }
}
