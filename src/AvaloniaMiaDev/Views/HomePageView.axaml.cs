using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
using AvaloniaMiaDev.Services.Camera.Models;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.Views;

public partial class HomePageView : UserControl
{
    private readonly IInferenceService _inferenceService;
    private readonly HomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private CamViewMode _leftCamViewMode = CamViewMode.Tracking;
    private Point? _leftCropStartPoint;
    private Rect? _leftCropRectangle;
    private bool _isLeftCropping;

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

        try
        {
            var cameraEntries = DeviceEnumerator.ListCameraNames();
            LeftCameraAddressEntry.ItemsSource = cameraEntries;
        }
        catch (Exception)
        {
            // Insufficient perms, ignore
        }

        LeftMouthWindow.PointerPressed += LeftOnPointerPressed;
        LeftMouthWindow.PointerMoved += LeftOnPointerMoved;
        LeftMouthWindow.PointerReleased += LeftOnPointerReleased;

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

        Image image = sender as Image;
        var position = e.GetPosition(LeftMouthWindow);
        var x = Math.Min(_leftCropStartPoint.Value.X, position.X);
        var y = Math.Min(_leftCropStartPoint.Value.Y, position.Y);
        var clampedWidth = Math.Clamp(Math.Abs(_leftCropStartPoint.Value.X - position.X), 0, image.Width - _leftCropStartPoint.Value.X);
        var clampedHeight = Math.Clamp(Math.Abs(_leftCropStartPoint.Value.Y - position.Y), 0, image.Height - _leftCropStartPoint.Value.Y);

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

    private void StartImageUpdates()
    {
        // Configure left rectangle
        LeftRectangleWindow.Stroke = Brushes.Red;
        LeftRectangleWindow.StrokeThickness = 2;

        DispatcherTimer drawTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        drawTimer.Tick += async (s, e) =>
        {
            await UpdateLeftImage();
        };
        drawTimer.Start();
    }

    private async Task UpdateLeftImage()
    {
        var isCroppingModeUiVisible = _leftCamViewMode == CamViewMode.Cropping;
        LeftRectangleWindow.IsVisible = isCroppingModeUiVisible;
        LeftSelectEntireFrame.IsVisible = isCroppingModeUiVisible;
        LeftViewBox.MaxHeight = isCroppingModeUiVisible ? double.MaxValue : 256;
        LeftViewBox.MaxWidth = isCroppingModeUiVisible ? double.MaxValue : 256;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        CameraSettings cameraSettings;
        if (_leftCropRectangle.HasValue)
        {
            cameraSettings = new CameraSettings
            {
                Chirality = Chirality.Left,
                RoiX = (int)_leftCropRectangle.Value.X,
                RoiY = (int)_leftCropRectangle.Value.Y,
                RoiWidth = (int)_leftCropRectangle.Value.Width,
                RoiHeight = (int)_leftCropRectangle.Value.Height,
                RotationRadians = await _localSettingsService.ReadSettingAsync<float>("EyeSettings_LeftEyeRotation"),
                UseHorizontalFlip = await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeXAxis"),
                UseVerticalFlip = await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeYAxis"),
                Brightness = 1f
            };
        }
        else
        {
            cameraSettings = new CameraSettings
            {
                Chirality = Chirality.Left,
                RoiX = 0,
                RoiY = 0,
                RoiWidth = 0,
                RoiHeight = 0,
                RotationRadians = await _localSettingsService.ReadSettingAsync<float>("EyeSettings_LeftEyeRotation"),
                UseHorizontalFlip = await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeXAxis"),
                UseVerticalFlip = await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeYAxis"),
                Brightness = 1f
            };
        }

        switch (_leftCamViewMode)
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

        if (valid && _isVisible)
        {
            LeftViewBox.Margin = new Thickness(0, 0, 0, 16);

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
            LeftViewBox.Margin = new Thickness();
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
}
