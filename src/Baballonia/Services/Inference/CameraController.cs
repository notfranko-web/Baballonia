using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Services.Inference.Enums;
using Baballonia.Services.Inference.Models;
using Baballonia.Views;
using OpenCvSharp;
using Rect = Avalonia.Rect;

namespace Baballonia.Services.Inference;

public class CameraController : IDisposable
{
    public CameraSettings CameraSettings { get; set; }

    public float[] ArExpressions = [];

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInferenceService _inferenceService;
    private readonly Camera _camera;

    // State
    private CamViewMode _camViewMode = CamViewMode.Tracking;
    public CamViewMode CamViewMode => _camViewMode;
    private Rect _overlayRectangle;
    private WriteableBitmap _bitmap;

    /// <summary>
    /// Force an image resize if we go from cropping to tracking mode or vice versa
    /// This handles the edge case where the dim size doesn't change, but color space does
    /// </summary>
    private bool _edgeCaseFlip;

    private (int, int) CameraSize { get; set; } = (0, 0);

    private MjpegStreamingService _mjpegStreamingService;
    public CropManager CropManager { get; } = new();

    public CameraController(
        ILocalSettingsService localSettingsService,
        IInferenceService inferenceService,
        Camera camera,
        CameraSettings cameraSettings)
    {
        _localSettingsService = localSettingsService;
        _inferenceService = inferenceService;
        _camera = camera;
        CameraSettings = cameraSettings;

        // Configure rectangle
        // _rectangleWindow.Stroke = Brushes.Red;
        // _rectangleWindow.StrokeThickness = 2;

        _mjpegStreamingService = new MjpegStreamingService();
    }

    public async Task UpdateImage(Action<WriteableBitmap> callback)
    {
        bool valid;
        bool useColor;
        Mat? image;

        _overlayRectangle = CropManager.CropZone;

        if (_overlayRectangle is { X: 0, Y: 0, Width: 0, Height: 0 })
        {
            var x = CameraSettings.RoiX;
            var y = CameraSettings.RoiY;
            var width = CameraSettings.RoiWidth;
            var height = CameraSettings.RoiHeight;
            _overlayRectangle = new Rect(x, y, width, height);
            CropManager.SetCropZone(_overlayRectangle);
        }

        CameraSettings.RoiX = (int) _overlayRectangle.X;
        CameraSettings.RoiY = (int) _overlayRectangle.Y;
        CameraSettings.RoiWidth = (int) _overlayRectangle.Width;
        CameraSettings.RoiHeight = (int) _overlayRectangle.Height;

        switch (_camViewMode)
        {
            case CamViewMode.Tracking:
                useColor = false;
                valid = _inferenceService.GetImage(CameraSettings, out image);
                if (valid) // Don't run infer on raw images
                {
                    CameraSize = (image.Width, image.Height);
                    _inferenceService.GetExpressionData(CameraSettings, out ArExpressions);
                }
                break;
            case CamViewMode.Cropping:
                useColor = true;
                valid = _inferenceService.GetRawImage(CameraSettings, ColorType.Bgr24, out image);
                CameraSize = (image.Width, image.Height);
                CropManager.CameraSize.Width = image.Width;
                CropManager.CameraSize.Height = image.Height;
                break;
            default:
                return;
        }

        if (valid)
        {
            if (CameraSize.Item1 == 0 || CameraSize.Item2 == 0 || image is null)
            {
                callback(null);
                return;
            }

            if (CameraSettings.RoiWidth == 0 || CameraSettings.RoiHeight == 0)
            {
                CropManager.SelectEntireFrame(_camera);
            }

            // Create or update bitmap if needed
            if (_bitmap is null ||
                _edgeCaseFlip ||
                _bitmap.PixelSize.Width != CameraSize.Item1 ||
                _bitmap.PixelSize.Height != CameraSize.Item2)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize(CameraSize.Item1, CameraSize.Item2),
                    new Vector(96, 96),
                    useColor ? PixelFormats.Bgr24 : PixelFormats.Gray8,
                    AlphaFormat.Opaque);
                // _imageWindow.Source = _bitmap;
            }

            // Allocation-free image-update
            using var frameBuffer = _bitmap.Lock();
            {
                IntPtr srcPtr = image.Data;
                IntPtr destPtr = frameBuffer.Address;
                int size = image.Rows * image.Cols * image.ElemSize();

                unsafe
                {
                    Buffer.MemoryCopy(srcPtr.ToPointer(), destPtr.ToPointer(), size, size);
                }
            }

            // Update MJPEG frame
            UpdateMjpegFrame(image);
            callback(_bitmap);
            return;
        }

        callback(null);

    }

    public void StartCamera(string cameraAddress)
    {
        if (!string.IsNullOrEmpty(cameraAddress))
        {
            StopCamera();
        }
        _inferenceService.SetupInference(_camera, cameraAddress);
    }

    public void StopCamera()
    {
        _inferenceService.Shutdown(_camera);
    }

    public void SetTrackingMode()
    {
        _camViewMode = CamViewMode.Tracking;
        _edgeCaseFlip = true;
    }

    public void SetCroppingMode()
    {
        _camViewMode = CamViewMode.Cropping;
        _edgeCaseFlip = true;
    }

    #region MJPEG Streaming

    /// <summary>
    /// Start MJPEG streaming server on the specified port
    /// </summary>
    /// <param name="port">Port to listen on (default: 8080)</param>
    public void StartMjpegStreaming(int port = 8080)
    {
        _mjpegStreamingService.StartStreaming(port);
    }

    private void UpdateMjpegFrame(Mat mat)
    {
        _mjpegStreamingService.UpdateMjpegFrame(mat);
    }

    public void StopMjpegStreaming()
    {
        _mjpegStreamingService.StopStreaming();
    }

    #endregion

    public void Dispose()
    {
        _mjpegStreamingService.Dispose();
        StopCamera();

        _bitmap?.Dispose();
    }
}
