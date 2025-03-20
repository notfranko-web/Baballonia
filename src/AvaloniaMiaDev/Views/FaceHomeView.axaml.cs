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

public partial class FaceHomeView : UserControl
{
    private readonly IInferenceService _inferenceService;
    private readonly FaceHomeViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private CamViewMode _faceCamViewMode = CamViewMode.Tracking;
    private Rect _faceOverlayRectangle;
    private bool _isFaceCropping;

    private double _dragStartX;
    private double _dragStartY;

    private bool _isVisible;

    public FaceHomeView()
    {
        InitializeComponent();
        Loaded += CamView_OnLoaded;
        Unloaded += CamView_Unloaded;

        _viewModel = Ioc.Default.GetRequiredService<FaceHomeViewModel>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _inferenceService = Ioc.Default.GetService<IInferenceService>()!;
        _localSettingsService.Load(this);

        try
        {
            var cameraEntries = DeviceEnumerator.ListCameraNames();
            FaceCameraAddressEntry.ItemsSource = cameraEntries;
        }
        catch (Exception)
        {
            // Insufficient perms, ignore
        }

        FaceMouthWindow.PointerPressed += FaceOnPointerPressed;
        FaceMouthWindow.PointerMoved += FaceOnPointerMoved;
        FaceMouthWindow.PointerReleased += FaceOnPointerReleased;

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

    private async void FaceOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_faceCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(FaceMouthWindow);
        _dragStartX = position.X;
        _dragStartY = position.Y;

        await _localSettingsService.SaveSettingAsync("Babble_FaceCameraROI", _faceOverlayRectangle);
        _isFaceCropping = true;
    }

    private async void FaceOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isFaceCropping) return;

        Image? image = sender as Image;

        var position = e.GetPosition(FaceMouthWindow);

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

        _faceOverlayRectangle = new Rect(x, y, Math.Min(image!.Width, w), Math.Min(image.Height, h));

        await _localSettingsService.SaveSettingAsync("Babble_FaceCameraROI", _faceOverlayRectangle);
    }

    private void FaceOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isFaceCropping) return;

        _isFaceCropping = false;
    }

    private void StartImageUpdates()
    {
        // Configure face rectangle
        FaceRectangleWindow.Stroke = Brushes.Red;
        FaceRectangleWindow.StrokeThickness = 2;

        DispatcherTimer drawTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        drawTimer.Tick += async (s, e) =>
        {
            await UpdateImage(Chirality.Face, _faceCamViewMode, FaceRectangleWindow, FaceSelectEntireFrame, FaceViewBox,
                _faceOverlayRectangle, FaceMouthWindow, FaceCanvasWindow);
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
            RotationRadians = await _localSettingsService.ReadSettingAsync<float>("Babble_FaceRotation"),
            UseHorizontalFlip = await _localSettingsService.ReadSettingAsync<bool>("Babble_FlipFaceXAxis"),
            UseVerticalFlip = await _localSettingsService.ReadSettingAsync<bool>("Babble_FlipFaceYAxis"),
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

            if (_viewModel.FaceBitmap is null ||
                _viewModel.FaceBitmap.PixelSize.Width != dims.width ||
                _viewModel.FaceBitmap.PixelSize.Height != dims.height)
            {
                _viewModel.FaceBitmap = new WriteableBitmap(
                    new PixelSize(dims.width, dims.height),
                    new Vector(96, 96),
                    useColor ? PixelFormats.Bgr24 : PixelFormats.Gray8,
                    AlphaFormat.Opaque);
                FaceMouthWindow.Source = _viewModel.FaceBitmap;
            }

            using var frameBuffer = _viewModel.FaceBitmap.Lock();
            {
                Marshal.Copy(image, 0, frameBuffer.Address, image.Length);
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

    public void FaceCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Face, _viewModel.FaceCameraAddress);
    }

    public void RightCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Face, _viewModel.FaceCameraAddress);
    }

    public void FaceOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        _faceCamViewMode = CamViewMode.Tracking;
        _isFaceCropping = false;
        FaceOnPointerReleased(null, null!); // Close and save any open crops
    }

    public void FaceOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        _faceCamViewMode = CamViewMode.Cropping;
    }

    public void FaceSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        if (_viewModel.FaceBitmap is null) return;

        _faceOverlayRectangle =
            new Rect(0, 0, _viewModel.FaceBitmap.Size.Width, _viewModel.FaceBitmap.Size.Height);
    }
}
