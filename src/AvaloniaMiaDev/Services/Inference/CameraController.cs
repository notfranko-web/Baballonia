using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Helpers;
using AvaloniaMiaDev.Services.Inference.Enums;
using AvaloniaMiaDev.Services.Inference.Models;
using AvaloniaMiaDev.Views;

namespace AvaloniaMiaDev.Services.Inference;

public class CameraController
{
    private readonly HomePageView _view;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInferenceService _inferenceService;
    private readonly Camera _camera;

    // UI Elements
    private readonly Rectangle _rectangleWindow;
    private readonly Button _selectEntireFrameButton;
    private readonly Viewbox _viewBox;
    private readonly Image _mouthWindow;
    private readonly Canvas _canvas;

    // State
    private CamViewMode _camViewMode = CamViewMode.Tracking;
    private Rect _overlayRectangle;
    private bool _isCropping;
    private double _dragStartX;
    private double _dragStartY;
    private WriteableBitmap _bitmap;

    // Settings keys
    private readonly string _roiSettingKey;
    private readonly string _rotationSettingKey;
    private readonly string _flipXSettingKey;
    private readonly string _flipYSettingKey;

    // Tracking mode property
    private readonly StyledProperty<bool> _isTrackingModeProperty;

    public CameraController(
        HomePageView view,
        ILocalSettingsService localSettingsService,
        IInferenceService inferenceService,
        Camera camera,
        Rectangle rectangleWindow,
        Button selectEntireFrameButton,
        Viewbox viewBox,
        Image mouthWindow,
        Canvas canvas,
        string roiSettingKey,
        string rotationSettingKey,
        string flipXSettingKey,
        string flipYSettingKey,
        StyledProperty<bool> isTrackingModeProperty)
    {
        _view = view;
        _localSettingsService = localSettingsService;
        _inferenceService = inferenceService;
        _camera = camera;
        _rectangleWindow = rectangleWindow;
        _selectEntireFrameButton = selectEntireFrameButton;
        _viewBox = viewBox;
        _mouthWindow = mouthWindow;
        _canvas = canvas;
        _roiSettingKey = roiSettingKey;
        _rotationSettingKey = rotationSettingKey;
        _flipXSettingKey = flipXSettingKey;
        _flipYSettingKey = flipYSettingKey;
        _isTrackingModeProperty = isTrackingModeProperty;

        // Set up event handlers
        _mouthWindow.PointerPressed += OnPointerPressed;
        _mouthWindow.PointerMoved += OnPointerMoved;
        _mouthWindow.PointerReleased += OnPointerReleased;

        // Configure rectangle
        _rectangleWindow.Stroke = Brushes.Red;
        _rectangleWindow.StrokeThickness = 2;

        // Set initial mode
        _view.SetValue(_isTrackingModeProperty, true);
    }

    public async Task UpdateImage(bool isVisible)
    {
        var isCroppingModeUiVisible = _camViewMode == CamViewMode.Cropping;
        _rectangleWindow.IsVisible = isCroppingModeUiVisible;
        _selectEntireFrameButton.IsVisible = isCroppingModeUiVisible;
        _viewBox.MaxHeight = isCroppingModeUiVisible ? double.MaxValue : 192;
        _viewBox.MaxWidth = isCroppingModeUiVisible ? double.MaxValue : 192;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        var cameraSettings = new CameraSettings
        {
            Camera = _camera,
            RoiX = (int)_overlayRectangle.X,
            RoiY = (int)_overlayRectangle.Y,
            RoiWidth = (int)_overlayRectangle.Width,
            RoiHeight = (int)_overlayRectangle.Height,
            RotationRadians = await _localSettingsService.ReadSettingAsync<float>(_rotationSettingKey),
            UseHorizontalFlip = await _localSettingsService.ReadSettingAsync<bool>(_flipXSettingKey),
            UseVerticalFlip = await _localSettingsService.ReadSettingAsync<bool>(_flipYSettingKey),
            Brightness = 1f
        };

        switch (_camViewMode)
        {
            case CamViewMode.Tracking:
                useColor = false;
                valid = _inferenceService.GetImage(cameraSettings, out image, out dims);
                break;
            case CamViewMode.Cropping:
                useColor = true;
                valid = _inferenceService.GetRawImage(cameraSettings, ColorType.Bgr24, out image, out dims);
                break;
            default:
                return;
        }

        if (valid && isVisible)
        {
            _viewBox.Margin = new Thickness(0, 0, 0, 16);

            if (dims.width == 0 || dims.height == 0 || image is null ||
                double.IsNaN(_mouthWindow.Width) || double.IsNaN(_mouthWindow.Height))
            {
                ResetViewSizes();
                return;
            }

            // Create or update bitmap if needed
            if (_bitmap is null ||
                _bitmap.PixelSize.Width != dims.width ||
                _bitmap.PixelSize.Height != dims.height)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize(dims.width, dims.height),
                    new Vector(96, 96),
                    useColor ? PixelFormats.Bgr24 : PixelFormats.Gray8,
                    AlphaFormat.Opaque);
                _mouthWindow.Source = _bitmap;
            }

            using var frameBuffer = _bitmap.Lock();
            {
                Marshal.Copy(image, 0, frameBuffer.Address, image.Length);
            }

            if (_mouthWindow.Width != dims.width || _mouthWindow.Height != dims.height)
            {
                _mouthWindow.Width = dims.width;
                _mouthWindow.Height = dims.height;
                _canvas.Width = dims.width;
                _canvas.Height = dims.height;
            }

            _rectangleWindow.Width = _overlayRectangle.Width;
            _rectangleWindow.Height = _overlayRectangle.Height;
            Canvas.SetLeft(_rectangleWindow, _overlayRectangle.X);
            Canvas.SetTop(_rectangleWindow, _overlayRectangle.Y);
        }
        else
        {
            ResetViewSizes();
        }

        Dispatcher.UIThread.Post(_mouthWindow.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(_canvas.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(_rectangleWindow.InvalidateVisual, DispatcherPriority.Render);
    }

    private void ResetViewSizes()
    {
        _viewBox.Margin = new Thickness();
        _mouthWindow.Width = 0;
        _mouthWindow.Height = 0;
        _canvas.Width = 0;
        _canvas.Height = 0;
        _rectangleWindow.Width = 0;
        _rectangleWindow.Height = 0;
    }

    public WriteableBitmap Bitmap => _bitmap;

    public void ConfigureCamera(string cameraAddress)
    {
        _inferenceService.ConfigurePlatformConnectors(_camera, cameraAddress);
    }

    public void SetTrackingMode()
    {
        _camViewMode = CamViewMode.Tracking;
        _isCropping = false;
        _view.SetValue(_isTrackingModeProperty, true);
        OnPointerReleased(null, null!); // Close and save any open crops
    }

    public void SetCroppingMode()
    {
        _camViewMode = CamViewMode.Cropping;
        _view.SetValue(_isTrackingModeProperty, false);
    }

    public void SelectEntireFrame()
    {
        if (_bitmap is null) return;

        _overlayRectangle = new Rect(0, 0, _bitmap.Size.Width, _bitmap.Size.Height);
    }

    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_camViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(_mouthWindow);
        _dragStartX = position.X;
        _dragStartY = position.Y;

        await _localSettingsService.SaveSettingAsync(_roiSettingKey, _overlayRectangle);
        _isCropping = true;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isCropping) return;

        Image? image = sender as Image;

        var position = e.GetPosition(_mouthWindow);

        double x, y, w, h;

        if (position.X < _dragStartX)
        {
            x = position.X;
            w = _dragStartX - x;
        }
        else
        {
            x = _dragStartX;
            w = position.X - _dragStartX;
        }

        if (position.Y < _dragStartY)
        {
            y = position.Y;
            h = _dragStartY - y;
        }
        else
        {
            y = _dragStartY;
            h = position.Y - _dragStartY;
        }

        _overlayRectangle = new Rect(x, y, Math.Min(image!.Width, w), Math.Min(image.Height, h));

        await _localSettingsService.SaveSettingAsync(_roiSettingKey, _overlayRectangle);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isCropping) return;

        _isCropping = false;
    }
}
