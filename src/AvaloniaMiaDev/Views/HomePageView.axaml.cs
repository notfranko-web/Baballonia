using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Helpers;
using AvaloniaMiaDev.Services.Camera.Enums;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.Views;

public partial class HomePageView : UserControl
{
    private readonly IInferenceService _inferenceService;
    private readonly HomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private CamViewMode _leftCamViewMode = CamViewMode.Tracking;
    private CamViewMode _rightCamViewMode = CamViewMode.Tracking;

    private Point? _leftCropStartPoint;
    private Point? _rightCropStartPoint;

    private Rect? _leftCropRectangle;
    private Rect? _rightCropRectangle;

    private bool _isLeftCropping;
    private bool _isRightCropping;

    private bool _isVisible;

    public HomePageView()
    {
        InitializeComponent();
        Loaded += CamView_OnLoaded;
        Unloaded += CamView_Unloaded;

        _viewModel = Ioc.Default.GetRequiredService<HomePageViewModel>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _inferenceService = Ioc.Default.GetService<IInferenceService>()!;
        _localSettingsService.Load(this);

        var cameraEntries = DeviceEnumerator.ListCameraNames();
        LeftCameraAddressEntry.ItemsSource = cameraEntries;
        RightCameraAddressEntry.ItemsSource = cameraEntries;

        LeftMouthWindow.PointerPressed += LeftOnPointerPressed;
        LeftMouthWindow.PointerMoved += LeftOnPointerMoved;
        LeftMouthWindow.PointerReleased += LeftOnPointerReleased;

        RightMouthWindow.PointerPressed += RightOnPointerPressed;
        RightMouthWindow.PointerMoved += RightOnPointerMoved;
        RightMouthWindow.PointerReleased += RightOnPointerReleased;

        StartImageUpdates();

        PropertyChanged += (_, _) =>
        {
            _localSettingsService.Save(this);
        };
    }

    private void CamView_OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isVisible = true;
    }

    private void CamView_Unloaded(object? sender, RoutedEventArgs e)
    {
        _isVisible = false;
    }

    #region Left Eye Events
    private void LeftOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_leftCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(LeftMouthWindow);
        _leftCropStartPoint = position;
        _leftCropRectangle = new Rect(position.X, position.Y, 0, 0);
        _isLeftCropping = true;
    }

    private void LeftOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isLeftCropping || _leftCropStartPoint is null) return;

        var position = e.GetPosition(LeftMouthWindow);
        var x = Math.Min(_leftCropStartPoint.Value.X, position.X);
        var y = Math.Min(_leftCropStartPoint.Value.Y, position.Y);
        var clampedWidth = Math.Clamp(Math.Abs(_leftCropStartPoint.Value.X - position.X), 0, _viewModel.LeftEyeBitmap.Size.Width - _leftCropStartPoint.Value.X);
        var clampedHeight = Math.Clamp(Math.Abs(_leftCropStartPoint.Value.Y - position.Y), 0, _viewModel.LeftEyeBitmap.Size.Height - _leftCropStartPoint.Value.Y);

        _leftCropRectangle = new Rect(x, y, clampedWidth, clampedHeight);
    }

    private void LeftOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isLeftCropping) return;

        _isLeftCropping = false;

        if (_leftCropStartPoint.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowX", _leftCropStartPoint.Value.X);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowY", _leftCropStartPoint.Value.Y);
        }

        if (_leftCropRectangle.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowW", _leftCropRectangle.Value.Width);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowH", _leftCropRectangle.Value.Height);
        }
    }
    #endregion

    #region Right Eye Events
    private void RightOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_rightCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(RightMouthWindow);
        _rightCropStartPoint = position;
        _rightCropRectangle = new Rect(position.X, position.Y, 0, 0);
        _isRightCropping = true;
    }

    private void RightOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isRightCropping || _rightCropStartPoint is null) return;

        var position = e.GetPosition(RightMouthWindow);
        var x = Math.Min(_rightCropStartPoint.Value.X, position.X);
        var y = Math.Min(_rightCropStartPoint.Value.Y, position.Y);
        var clampedWidth = Math.Clamp(Math.Abs(_rightCropStartPoint.Value.X - position.X), 0, _viewModel.RightEyeBitmap.Size.Width - _rightCropStartPoint.Value.X);
        var clampedHeight = Math.Clamp(Math.Abs(_rightCropStartPoint.Value.Y - position.Y), 0, _viewModel.RightEyeBitmap.Size.Height - _rightCropStartPoint.Value.Y);

        _rightCropRectangle = new Rect(x, y, clampedWidth, clampedHeight);
    }

    private void RightOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isRightCropping) return;

        _isRightCropping = false;

        if (_rightCropStartPoint.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowX", _rightCropStartPoint.Value.X);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowY", _rightCropStartPoint.Value.Y);
        }

        if (_rightCropRectangle.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowW", _rightCropRectangle.Value.Width);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowH", _rightCropRectangle.Value.Height);
        }
    }
    #endregion

    private void StartImageUpdates()
    {
        // Configure left rectangle
        LeftRectangleWindow.Stroke = Brushes.Red;
        LeftRectangleWindow.StrokeThickness = 2;

        // Configure right rectangle
        RightRectangleWindow.Stroke = Brushes.Red;
        RightRectangleWindow.StrokeThickness = 2;

        DispatcherTimer drawTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        drawTimer.Tick += (s, e) =>
        {
            UpdateLeftImage();
            UpdateRightImage();
        };
        drawTimer.Start();
    }

    private void UpdateLeftImage()
    {
        var isCroppingModeUiVisible = _leftCamViewMode == CamViewMode.Cropping;
        LeftRectangleWindow.IsVisible = isCroppingModeUiVisible;
        LeftSelectEntireFrame.IsVisible = isCroppingModeUiVisible;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        switch (_leftCamViewMode)
        {
            case CamViewMode.Tracking:
                useColor = false;
                valid = _inferenceService.GetImage(Chirality.Left, out image, out dims);
                break;
            case CamViewMode.Cropping:
                useColor = true;
                valid = _inferenceService.GetRawImage(Chirality.Left, ColorType.Bgr24, out image, out dims);
                break;
            default:
                return;
        }

        if (valid && _isVisible)
        {
            if (dims.width == 0 || dims.height == 0 || image is null ||
                double.IsNaN(LeftMouthWindow.Width) || double.IsNaN(LeftMouthWindow.Height))
            {
                LeftMouthWindow.Width = 0;
                LeftMouthWindow.Height = 0;
                LeftCanvasWindow.Width = 0;
                LeftCanvasWindow.Height = 0;
                LeftRectangleWindow.Width = 0;
                LeftRectangleWindow.Height = 0;
                Dispatcher.UIThread.Post(LeftMouthWindow.InvalidateVisual, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(LeftCanvasWindow.InvalidateVisual, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(LeftRectangleWindow.InvalidateVisual, DispatcherPriority.Render);
                return;
            }

            if (_viewModel.LeftEyeBitmap is null ||
                _viewModel.LeftEyeBitmap.PixelSize.Width != dims.width ||
                _viewModel.LeftEyeBitmap.PixelSize.Height != dims.height)
            {
                _viewModel.LeftEyeBitmap = new WriteableBitmap(
                    new PixelSize(dims.width, dims.height),
                    new Vector(96, 96),
                    useColor ? PixelFormats.Bgr24 : PixelFormats.Gray8,
                    AlphaFormat.Opaque);
                LeftMouthWindow.Source = _viewModel.LeftEyeBitmap;
            }

            using var frameBuffer = _viewModel.LeftEyeBitmap.Lock();
            {
                Marshal.Copy(image, 0, frameBuffer.Address, image.Length);
            }

            if (LeftMouthWindow.Width != dims.width || LeftMouthWindow.Height != dims.height)
            {
                LeftMouthWindow.Width = dims.width;
                LeftMouthWindow.Height = dims.height;
                LeftCanvasWindow.Width = dims.width;
                LeftCanvasWindow.Height = dims.height;
            }

            if (_leftCropRectangle.HasValue)
            {
                LeftRectangleWindow.Width = _leftCropRectangle.Value.Width;
                LeftRectangleWindow.Height = _leftCropRectangle.Value.Height;
            }

            if (_leftCropStartPoint.HasValue)
            {
                _viewModel.LeftOverlayRectangleCanvasX = ((int)_leftCropStartPoint.Value.X);
                _viewModel.LeftOverlayRectangleCanvasY = ((int)_leftCropStartPoint.Value.Y);
            }
        }
        else
        {
            LeftMouthWindow.Width = 0;
            LeftMouthWindow.Height = 0;
            LeftCanvasWindow.Width = 0;
            LeftCanvasWindow.Height = 0;
            LeftRectangleWindow.Width = 0;
            LeftRectangleWindow.Height = 0;
        }

        Dispatcher.UIThread.Post(LeftMouthWindow.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(LeftCanvasWindow.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(LeftRectangleWindow.InvalidateVisual, DispatcherPriority.Render);
    }

    private void UpdateRightImage()
    {
        var isCroppingModeUiVisible = _rightCamViewMode == CamViewMode.Cropping;
        RightRectangleWindow.IsVisible = isCroppingModeUiVisible;
        RightSelectEntireFrame.IsVisible = isCroppingModeUiVisible;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        switch (_rightCamViewMode)
        {
            case CamViewMode.Tracking:
                useColor = false;
                RightPerfText.IsVisible = true;
                valid = _inferenceService.GetImage(Chirality.Right, out image, out dims);
                break;
            case CamViewMode.Cropping:
                useColor = true;
                RightPerfText.IsVisible = false;
                valid = _inferenceService.GetRawImage(Chirality.Right, ColorType.Bgr24, out image, out dims);
                break;
            default:
                return;
        }

        if (valid && _isVisible)
        {
            if (dims.width == 0 || dims.height == 0 || image is null ||
                double.IsNaN(RightMouthWindow.Width) || double.IsNaN(RightMouthWindow.Height))
            {
                RightMouthWindow.Width = 0;
                RightMouthWindow.Height = 0;
                RightCanvasWindow.Width = 0;
                RightCanvasWindow.Height = 0;
                RightRectangleWindow.Width = 0;
                RightRectangleWindow.Height = 0;
                Dispatcher.UIThread.Post(RightMouthWindow.InvalidateVisual, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(RightCanvasWindow.InvalidateVisual, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(RightRectangleWindow.InvalidateVisual, DispatcherPriority.Render);
                return;
            }

            if (_viewModel.RightEyeBitmap is null ||
                _viewModel.RightEyeBitmap.PixelSize.Width != dims.width ||
                _viewModel.RightEyeBitmap.PixelSize.Height != dims.height)
            {
                _viewModel.RightEyeBitmap = new WriteableBitmap(
                    new PixelSize(dims.width, dims.height),
                    new Vector(96, 96),
                    useColor ? PixelFormats.Bgr24 : PixelFormats.Gray8,
                    AlphaFormat.Opaque);
                RightMouthWindow.Source = _viewModel.RightEyeBitmap;

            }

            if (_inferenceService.Ms > 0)
            {
                RightPerfText.Text = $"FPS: {_inferenceService.Fps} MS: {_inferenceService.Ms:F2}";
            }
            else
            {
                RightPerfText.Text = $"FPS: -- MS: --.--";
            }

            using var frameBuffer = _viewModel.RightEyeBitmap.Lock();
            {
                Marshal.Copy(image, 0, frameBuffer.Address, image.Length);
            }

            if (RightMouthWindow.Width != dims.width || RightMouthWindow.Height != dims.height)
            {
                RightMouthWindow.Width = dims.width;
                RightMouthWindow.Height = dims.height;
                RightCanvasWindow.Width = dims.width;
                RightCanvasWindow.Height = dims.height;
            }

            if (_rightCropRectangle.HasValue)
            {
                RightRectangleWindow.Width = _rightCropRectangle.Value.Width;
                RightRectangleWindow.Height = _rightCropRectangle.Value.Height;
            }

            if (_rightCropStartPoint.HasValue)
            {
                _viewModel.RightOverlayRectangleCanvasX = ((int)_rightCropStartPoint.Value.X);
                _viewModel.RightOverlayRectangleCanvasY = ((int)_rightCropStartPoint.Value.Y);
            }
        }
        else
        {
            RightPerfText.IsVisible = false;
            RightPerfText.Text = string.Empty;
            RightMouthWindow.Width = 0;
            RightMouthWindow.Height = 0;
            RightCanvasWindow.Width = 0;
            RightCanvasWindow.Height = 0;
            RightRectangleWindow.Width = 0;
            RightRectangleWindow.Height = 0;
        }

        Dispatcher.UIThread.Post(RightMouthWindow.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(RightCanvasWindow.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(RightRectangleWindow.InvalidateVisual, DispatcherPriority.Render);
    }

    #region Left Panel Button Events
    public void LeftCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Left, _viewModel.LeftCameraAddress);
    }

    public void LeftOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        _leftCamViewMode = CamViewMode.Tracking;
        _isLeftCropping = false;
        LeftOnPointerReleased(null, null!); // Close and save any open crops
    }

    public void LeftOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        _leftCamViewMode = CamViewMode.Cropping;
    }

    public void LeftSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        if (_viewModel.LeftEyeBitmap is null) return;

        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowX", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowY", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowW", _viewModel.LeftEyeBitmap.Size.Width);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowH", _viewModel.LeftEyeBitmap.Size.Height);
        _leftCropStartPoint = new Point(0, 0);
        _leftCropRectangle = new Rect(0, 0, _viewModel.LeftEyeBitmap.Size.Width, _viewModel.LeftEyeBitmap.Size.Height);
    }
    #endregion

    #region Right Panel Button Events
    public void RightCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Right, _viewModel.RightCameraAddress);
    }

    public void RightOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        _rightCamViewMode = CamViewMode.Tracking;
        _isRightCropping = false;
        RightOnPointerReleased(null, null!); // Close and save any open crops
    }

    public void RightOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        _rightCamViewMode = CamViewMode.Cropping;
    }

    public void RightSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        if (_viewModel.RightEyeBitmap is null) return;

        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowX", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowY", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowW", _viewModel.RightEyeBitmap.Size.Width);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowH", _viewModel.RightEyeBitmap.Size.Height);
        _rightCropStartPoint = new Point(0, 0);
        _rightCropRectangle = new Rect(0, 0, _viewModel.RightEyeBitmap.Size.Width, _viewModel.RightEyeBitmap.Size.Height);
    }
    #endregion
}
