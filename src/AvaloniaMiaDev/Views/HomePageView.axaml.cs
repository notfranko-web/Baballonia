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
    private Rect _leftOverlayRectangle;
    private bool _isLeftCropping;

    private double _dragStartX;
    private double _dragStartY;

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

    private async void LeftOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_leftCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(LeftMouthWindow);
        _dragStartX = position.X;
        _dragStartY = position.Y;

        await _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftCameraROI", _leftOverlayRectangle);
        _isLeftCropping = true;
    }

    private async void LeftOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isLeftCropping) return;

        Image? image = sender as Image;

        var position = e.GetPosition(LeftMouthWindow);

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

        _leftOverlayRectangle = new Rect(x, y, Math.Min(image!.Width, w), Math.Min(image.Height, h));

        await _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftCameraROI", _leftOverlayRectangle);
    }

    private void LeftOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isLeftCropping) return;

        _isLeftCropping = false;
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

        var cameraSettings = new CameraSettings
        {
            Chirality = Chirality.Left,
            RoiX = (int)_leftOverlayRectangle.X,
            RoiY = (int)_leftOverlayRectangle.Y,
            RoiWidth = (int)_leftOverlayRectangle.Width,
            RoiHeight = (int)_leftOverlayRectangle.Height,
            RotationRadians = await _localSettingsService.ReadSettingAsync<float>("EyeSettings_LeftEyeRotation"),
            UseHorizontalFlip = await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeXAxis"),
            UseVerticalFlip = await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeYAxis"),
            Brightness = 1f
        };

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

            LeftRectangleWindow.Width = _leftOverlayRectangle.Width;
            LeftRectangleWindow.Height = _leftOverlayRectangle.Height;
            Canvas.SetLeft(LeftRectangleWindow, _leftOverlayRectangle.X);
            Canvas.SetTop(LeftRectangleWindow, _leftOverlayRectangle.Y);
        }
        else
        {
            LeftViewBox.Margin = new Thickness();
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

        _leftOverlayRectangle =
            new Rect(0, 0, _viewModel.LeftEyeBitmap.Size.Width, _viewModel.LeftEyeBitmap.Size.Height);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftROI", _leftOverlayRectangle);
    }
}
