using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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

public partial class EyeHomeView : UserControl
{
    private readonly IInferenceService _inferenceService;
    private readonly EyeHomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private CamViewMode _leftCamViewMode = CamViewMode.Tracking;
    private Rect _leftOverlayRectangle;
    private bool _isLeftCropping;

    private CamViewMode _rightCamViewMode = CamViewMode.Tracking;
    private Rect _rightOverlayRectangle;
    private bool _isRightCropping;

    private double _dragStartX;
    private double _dragStartY;

    private bool _isVisible;

    public EyeHomeView()
    {
        InitializeComponent();
        Loaded += CamView_OnLoaded;
        Unloaded += CamView_Unloaded;

        _viewModel = Ioc.Default.GetRequiredService<EyeHomePageViewModel>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _inferenceService = Ioc.Default.GetService<IInferenceService>()!;
        _localSettingsService.Load(this);

        try
        {
            var cameraEntries = DeviceEnumerator.ListCameraNames();
            LeftCameraAddressEntry.ItemsSource = cameraEntries;
            RightCameraAddressEntry.ItemsSource = cameraEntries;
        }
        catch (Exception)
        {
            // Insufficient perms, ignore
        }

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

    private async void RightOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_rightCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(RightMouthWindow);
        _dragStartX = position.X;
        _dragStartY = position.Y;

        await _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftCameraROI", _rightOverlayRectangle);
        _isRightCropping = true;
    }

    private async void RightOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isRightCropping) return;

        Image? image = sender as Image;

        var position = e.GetPosition(RightMouthWindow);

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

        _rightOverlayRectangle = new Rect(x, y, Math.Min(image!.Width, w), Math.Min(image.Height, h));

        await _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightCameraROI", _rightOverlayRectangle);
    }

    private void RightOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isRightCropping) return;

        _isRightCropping = false;
    }

    private void StartImageUpdates()
    {
        // Configure left rectangle
        LeftRectangleWindow.Stroke = Brushes.Red;
        LeftRectangleWindow.StrokeThickness = 2;

        RightRectangleWindow.Stroke = Brushes.Red;
        RightRectangleWindow.StrokeThickness = 2;

        DispatcherTimer drawTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        drawTimer.Tick += async (s, e) =>
        {
            await UpdateImage(Chirality.Left, _leftCamViewMode, LeftRectangleWindow, LeftSelectEntireFrame, LeftViewBox,
                _leftOverlayRectangle, LeftMouthWindow, LeftCanvasWindow);
            await UpdateImage(Chirality.Right, _rightCamViewMode, RightRectangleWindow, RightSelectEntireFrame, RightViewBox,
                _rightOverlayRectangle, RightMouthWindow, RightCanvasWindow);
        };
        drawTimer.Start();
    }

    private async Task UpdateImage(Chirality chirality, CamViewMode croppingMode, Rectangle rectWindow, Button selectEntireFrameButton,
        Viewbox viewBox, Rect overlayRectangle, Image mouthWindow, Canvas canvas)
    {
        var isCroppingModeUiVisible = croppingMode == CamViewMode.Cropping;
        rectWindow.IsVisible = isCroppingModeUiVisible;
        selectEntireFrameButton.IsVisible = isCroppingModeUiVisible;
        viewBox.MaxHeight = isCroppingModeUiVisible ? double.MaxValue : 256;
        viewBox.MaxWidth = isCroppingModeUiVisible ? double.MaxValue : 256;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        var cameraSettings = new CameraSettings
        {
            Chirality = chirality,
            RoiX = (int)overlayRectangle.X,
            RoiY = (int)overlayRectangle.Y,
            RoiWidth = (int)overlayRectangle.Width,
            RoiHeight = (int)overlayRectangle.Height,
            RotationRadians = chirality == Chirality.Left ?
                await _localSettingsService.ReadSettingAsync<float>("EyeSettings_LeftEyeRotation") :
                await _localSettingsService.ReadSettingAsync<float>("EyeSettings_RightEyeRotation"),
            UseHorizontalFlip = chirality == Chirality.Left ?
                await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeXAxis") :
                await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipRightEyeXAxis"),
            UseVerticalFlip = chirality == Chirality.Left ?
                await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipLeftEyeYAxis") :
                await _localSettingsService.ReadSettingAsync<bool>("EyeSettings_FlipRightEyeYAxis"),
            Brightness = 1f
        };

        switch (croppingMode)
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
            viewBox.Margin = new Thickness(0, 0, 0, 16);

            if (dims.width == 0 || dims.height == 0 || image is null ||
                double.IsNaN(mouthWindow.Width) || double.IsNaN(mouthWindow.Height))
            {
                mouthWindow.Width = 0;
                mouthWindow.Height = 0;
                canvas.Width = 0;
                canvas.Height = 0;
                rectWindow.Width = 0;
                rectWindow.Height = 0;
                Dispatcher.UIThread.Post(mouthWindow.InvalidateVisual, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(canvas.InvalidateVisual, DispatcherPriority.Render);
                Dispatcher.UIThread.Post(rectWindow.InvalidateVisual, DispatcherPriority.Render);
                return;
            }

            // Hacky!
            if (Chirality.Left == chirality)
            {
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
            }
            else if (Chirality.Right == chirality)
            {
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

                using var frameBuffer = _viewModel.RightEyeBitmap.Lock();
                {
                    Marshal.Copy(image, 0, frameBuffer.Address, image.Length);
                }
            }

            if (mouthWindow.Width != dims.width || mouthWindow.Height != dims.height)
            {
                mouthWindow.Width = dims.width;
                mouthWindow.Height = dims.height;
                canvas.Width = dims.width;
                canvas.Height = dims.height;
            }

            rectWindow.Width = overlayRectangle.Width;
            rectWindow.Height = overlayRectangle.Height;
            Canvas.SetLeft(rectWindow, overlayRectangle.X);
            Canvas.SetTop(rectWindow, overlayRectangle.Y);
        }
        else
        {
            viewBox.Margin = new Thickness();
            viewBox.Margin = new Thickness();
            mouthWindow.Width = 0;
            mouthWindow.Height = 0;
            canvas.Width = 0;
            canvas.Height = 0;
            rectWindow.Width = 0;
            rectWindow.Height = 0;
        }

        Dispatcher.UIThread.Post(mouthWindow.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(canvas.InvalidateVisual, DispatcherPriority.Render);
        Dispatcher.UIThread.Post(rectWindow.InvalidateVisual, DispatcherPriority.Render);
    }

    public void LeftCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Left, _viewModel.LeftCameraAddress);
    }

    public void RightCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Right, _viewModel.RightCameraAddress);
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

        _rightOverlayRectangle =
            new Rect(0, 0, _viewModel.RightEyeBitmap.Size.Width, _viewModel.RightEyeBitmap.Size.Height);
    }
}
