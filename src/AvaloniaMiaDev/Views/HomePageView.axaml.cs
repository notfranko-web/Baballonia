using System;
using System.Collections.Generic;
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
using AvaloniaMiaDev.Services;
using AvaloniaMiaDev.Services.Camera.Enums;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.Views;

public partial class HomePageView : UserControl
{
    private readonly IInferenceService _inferenceService;
    private readonly HomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private CamViewMode leftCamViewMode = CamViewMode.Tracking;
    private CamViewMode rightCamViewMode = CamViewMode.Tracking;

    private Point? leftCropStartPoint;
    private Point? rightCropStartPoint;

    private Rect? leftCropRectangle;
    private Rect? rightCropRectangle;

    private bool isLeftCropping;
    private bool isRightCropping;

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
        if (leftCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(LeftMouthWindow);
        leftCropStartPoint = position;
        leftCropRectangle = new Rect(position.X, position.Y, 0, 0);
        isLeftCropping = true;
    }

    private void LeftOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isLeftCropping || leftCropStartPoint is null) return;

        var position = e.GetPosition(LeftMouthWindow);
        var x = Math.Min(leftCropStartPoint.Value.X, position.X);
        var y = Math.Min(leftCropStartPoint.Value.Y, position.Y);
        var clampedWidth = Math.Clamp(Math.Abs(leftCropStartPoint.Value.X - position.X), 0, _viewModel.LeftEyeBitmap.Size.Width - leftCropStartPoint.Value.X);
        var clampedHeight = Math.Clamp(Math.Abs(leftCropStartPoint.Value.Y - position.Y), 0, _viewModel.LeftEyeBitmap.Size.Height - leftCropStartPoint.Value.Y);

        leftCropRectangle = new Rect(x, y, clampedWidth, clampedHeight);
    }

    private void LeftOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isLeftCropping) return;

        isLeftCropping = false;

        if (leftCropStartPoint.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowX", leftCropStartPoint.Value.X);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowY", leftCropStartPoint.Value.Y);
        }

        if (leftCropRectangle.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowW", leftCropRectangle.Value.Width);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowH", leftCropRectangle.Value.Height);
        }
    }
    #endregion

    #region Right Eye Events
    private void RightOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (rightCamViewMode != CamViewMode.Cropping) return;

        var position = e.GetPosition(RightMouthWindow);
        rightCropStartPoint = position;
        rightCropRectangle = new Rect(position.X, position.Y, 0, 0);
        isRightCropping = true;
    }

    private void RightOnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isRightCropping || rightCropStartPoint is null) return;

        var position = e.GetPosition(RightMouthWindow);
        var x = Math.Min(rightCropStartPoint.Value.X, position.X);
        var y = Math.Min(rightCropStartPoint.Value.Y, position.Y);
        var clampedWidth = Math.Clamp(Math.Abs(rightCropStartPoint.Value.X - position.X), 0, _viewModel.RightEyeBitmap.Size.Width - rightCropStartPoint.Value.X);
        var clampedHeight = Math.Clamp(Math.Abs(rightCropStartPoint.Value.Y - position.Y), 0, _viewModel.RightEyeBitmap.Size.Height - rightCropStartPoint.Value.Y);

        rightCropRectangle = new Rect(x, y, clampedWidth, clampedHeight);
    }

    private void RightOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isRightCropping) return;

        isRightCropping = false;

        if (rightCropStartPoint.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowX", rightCropStartPoint.Value.X);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowY", rightCropStartPoint.Value.Y);
        }

        if (rightCropRectangle.HasValue)
        {
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowW", rightCropRectangle.Value.Width);
            _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowH", rightCropRectangle.Value.Height);
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
        var isCroppingModeUIVisible = leftCamViewMode == CamViewMode.Cropping;
        LeftRectangleWindow.IsVisible = isCroppingModeUIVisible;
        LeftSelectEntireFrame.IsVisible = isCroppingModeUIVisible;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        switch (leftCamViewMode)
        {
            case CamViewMode.Tracking:
                useColor = false;
                LeftPerfText.IsVisible = true;
                valid = _inferenceService.GetImage(Chirality.Left, out image, out dims);
                break;
            case CamViewMode.Cropping:
                useColor = true;
                LeftPerfText.IsVisible = false;
                valid = _inferenceService.GetRawImage(Chirality.Left, ColorType.BGR_24, out image, out dims);
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
            }

            if (_inferenceService.MS > 0)
            {
                LeftPerfText.Text = $"FPS: {_inferenceService.FPS} MS: {_inferenceService.MS:F2}";
            }
            else
            {
                LeftPerfText.Text = $"FPS: -- MS: --.--";
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

            if (leftCropRectangle.HasValue)
            {
                LeftRectangleWindow.Width = leftCropRectangle.Value.Width;
                LeftRectangleWindow.Height = leftCropRectangle.Value.Height;
            }

            if (leftCropStartPoint.HasValue)
            {
                _viewModel.LeftOverlayRectangleCanvasX = ((int)leftCropStartPoint.Value.X);
                _viewModel.LeftOverlayRectangleCanvasY = ((int)leftCropStartPoint.Value.Y);
            }
        }
        else
        {
            LeftPerfText.IsVisible = false;
            LeftPerfText.Text = string.Empty;
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
        var isCroppingModeUIVisible = rightCamViewMode == CamViewMode.Cropping;
        RightRectangleWindow.IsVisible = isCroppingModeUIVisible;
        RightSelectEntireFrame.IsVisible = isCroppingModeUIVisible;

        bool valid;
        bool useColor;
        byte[]? image;
        (int width, int height) dims;

        switch (rightCamViewMode)
        {
            case CamViewMode.Tracking:
                useColor = false;
                RightPerfText.IsVisible = true;
                valid = _inferenceService.GetImage(Chirality.Right, out image, out dims);
                break;
            case CamViewMode.Cropping:
                useColor = true;
                RightPerfText.IsVisible = false;
                valid = _inferenceService.GetRawImage(Chirality.Right, ColorType.BGR_24, out image, out dims);
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
            }

            if (_inferenceService.MS > 0)
            {
                RightPerfText.Text = $"FPS: {_inferenceService.FPS} MS: {_inferenceService.MS:F2}";
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

            if (rightCropRectangle.HasValue)
            {
                RightRectangleWindow.Width = rightCropRectangle.Value.Width;
                RightRectangleWindow.Height = rightCropRectangle.Value.Height;
            }

            if (rightCropStartPoint.HasValue)
            {
                _viewModel.RightOverlayRectangleCanvasX = ((int)rightCropStartPoint.Value.X);
                _viewModel.RightOverlayRectangleCanvasY = ((int)rightCropStartPoint.Value.Y);
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
        leftCamViewMode = CamViewMode.Tracking;
        isLeftCropping = false;
        LeftOnPointerReleased(null, null); // Close and save any open crops
    }

    public void LeftOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        leftCamViewMode = CamViewMode.Cropping;
    }

    public void LeftSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        if (_viewModel.LeftEyeBitmap is null) return;

        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowX", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowY", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowW", _viewModel.LeftEyeBitmap.Size.Width);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_LeftRoiWindowH", _viewModel.LeftEyeBitmap.Size.Height);
        leftCropStartPoint = new Point(0, 0);
        leftCropRectangle = new Rect(0, 0, _viewModel.LeftEyeBitmap.Size.Width, _viewModel.LeftEyeBitmap.Size.Height);
    }
    #endregion

    #region Right Panel Button Events
    public void RightCameraAddressClicked(object? sender, RoutedEventArgs e)
    {
        _inferenceService.ConfigurePlatformConnectors(Chirality.Right, _viewModel.RightCameraAddress);
    }

    public void RightOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        rightCamViewMode = CamViewMode.Tracking;
        isRightCropping = false;
        RightOnPointerReleased(null, null); // Close and save any open crops
    }

    public void RightOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        rightCamViewMode = CamViewMode.Cropping;
    }

    public void RightSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        if (_viewModel.RightEyeBitmap is null) return;

        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowX", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowY", 0);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowW", _viewModel.RightEyeBitmap.Size.Width);
        _localSettingsService.SaveSettingAsync("EyeTrackVRService_RightRoiWindowH", _viewModel.RightEyeBitmap.Size.Height);
        rightCropStartPoint = new Point(0, 0);
        rightCropRectangle = new Rect(0, 0, _viewModel.RightEyeBitmap.Size.Width, _viewModel.RightEyeBitmap.Size.Height);
    }
    #endregion
}
