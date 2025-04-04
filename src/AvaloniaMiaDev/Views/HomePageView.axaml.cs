using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Helpers;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.Services.Inference;
using AvaloniaMiaDev.Services.Inference.Enums;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;
using Path = System.IO.Path;

namespace AvaloniaMiaDev.Views;

public partial class HomePageView : UserControl
{
    public static readonly StyledProperty<bool> IsLeftTrackingModeProperty =
        AvaloniaProperty.Register<HomePageView, bool>(nameof(IsLeftTrackingMode));

    public static readonly StyledProperty<bool> IsRightTrackingModeProperty =
        AvaloniaProperty.Register<HomePageView, bool>(nameof(IsRightTrackingMode));

    public static readonly StyledProperty<bool> IsFaceTrackingModeProperty =
        AvaloniaProperty.Register<HomePageView, bool>(nameof(IsFaceTrackingMode));

    public bool IsLeftTrackingMode
    {
        get => GetValue(IsLeftTrackingModeProperty);
        set => SetValue(IsLeftTrackingModeProperty, value);
    }

    public bool IsRightTrackingMode
    {
        get => GetValue(IsRightTrackingModeProperty);
        set => SetValue(IsRightTrackingModeProperty, value);
    }

    public bool IsFaceTrackingMode
    {
        get => GetValue(IsFaceTrackingModeProperty);
        set => SetValue(IsFaceTrackingModeProperty, value);
    }

    private readonly IInferenceService _inferenceService;
    private readonly IVRService _vrService;
    private readonly HomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private CameraController _leftCameraController;
    private CameraController _rightCameraController;
    private CameraController _faceCameraController;

    private bool _isVisible;

    public HomePageView()
    {
        InitializeComponent();
        Loaded += CamView_OnLoaded;
        Unloaded += CamView_Unloaded;

        _viewModel = Ioc.Default.GetRequiredService<HomePageViewModel>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _inferenceService = Ioc.Default.GetService<IInferenceService>()!;
        _vrService = Ioc.Default.GetService<IVRService>()!;
        _localSettingsService.Load(this);

        try
        {
            var cameraEntries = DeviceEnumerator.ListCameraNames();
            LeftCameraAddressEntry.ItemsSource = cameraEntries;
            RightCameraAddressEntry.ItemsSource = cameraEntries;
            FaceCameraAddressEntry.ItemsSource = cameraEntries;
        }
        catch (Exception)
        {
            // Insufficient perms, ignore
        }

        // Initialize camera controllers
        _leftCameraController = new CameraController(
            this,
            _localSettingsService,
            _inferenceService,
            Camera.Left,
            LeftRectangleWindow,
            LeftSelectEntireFrame,
            LeftViewBox,
            LeftMouthWindow,
            LeftCanvasWindow,
            "EyeHome_LeftCameraROIX",
            "EyeHome_LeftCameraROIY",
            "EyeHome_LeftCameraROIWidth",
            "EyeHome_LeftCameraROIHeight",
            "EyeHome_LeftEyeRotation",
            "EyeHome_FlipLeftEyeXAxis",
            "EyeHome_FlipLeftEyeYAxis",
            IsLeftTrackingModeProperty);

        _rightCameraController = new CameraController(
            this,
            _localSettingsService,
            _inferenceService,
            Camera.Right,
            RightRectangleWindow,
            RightSelectEntireFrame,
            RightViewBox,
            RightMouthWindow,
            RightCanvasWindow,
            "EyeHome_RightCameraROIX",
            "EyeHome_RightCameraROIY",
            "EyeHome_RightCameraROIWidth",
            "EyeHome_RightCameraROIHeight",
            "EyeHome_RightEyeRotation",
            "EyeHome_FlipRightEyeXAxis",
            "EyeHome_FlipRightEyeYAxis",
            IsRightTrackingModeProperty);

        _faceCameraController = new CameraController(
            this,
            _localSettingsService,
            _inferenceService,
            Camera.Face,
            FaceRectangleWindow,
            FaceSelectEntireFrame,
            FaceViewBox,
            FaceMouthWindow,
            FaceCanvasWindow,
            "Face_CameraROIX",
            "Face_CameraROIY",
            "Face_CameraROIWidth",
            "Face_CameraROIHeight",
            "Face_Rotation",
            "Face_FlipXAxis",
            "Face_FlipYAxis",
            IsFaceTrackingModeProperty);

        StartImageUpdates();

        PropertyChanged += (_, _) => { _localSettingsService.Save(this); };
    }

    private void CamView_OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isVisible = true;
    }

    private void CamView_Unloaded(object? sender, RoutedEventArgs e)
    {
        _leftCameraController.StopMjpegStreaming();
        _rightCameraController.StopMjpegStreaming();
        _isVisible = false;
    }

    private void StartImageUpdates()
    {
        DispatcherTimer drawTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        drawTimer.Tick += async (s, e) =>
        {
            await _leftCameraController.UpdateImage(_isVisible);
            await _rightCameraController.UpdateImage(_isVisible);
            await _faceCameraController.UpdateImage(_isVisible);

            // Update ViewModel bitmaps
            _viewModel.LeftEyeBitmap = _leftCameraController.Bitmap;
            _viewModel.RightEyeBitmap = _rightCameraController.Bitmap;
            _viewModel.FaceBitmap = _faceCameraController.Bitmap;
        };
        drawTimer.Start();
    }

    // Event handlers for left camera
    public void LeftCameraStart(object? sender, RoutedEventArgs e)
    {
        _leftCameraController.StartCamera(_viewModel.LeftCameraAddress);
    }

    private void LeftCameraStopped(object? sender, RoutedEventArgs e)
    {
        _leftCameraController.StopCamera(Camera.Left);
        _leftCameraController.StopMjpegStreaming();
    }

    public void LeftOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        _leftCameraController.SetTrackingMode();
    }

    public void LeftOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        _leftCameraController.SetCroppingMode();
    }

    public void LeftSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        _leftCameraController.SelectEntireFrame();
    }

    // Event handlers for right camera
    public void RightCameraStart(object? sender, RoutedEventArgs e)
    {
        _rightCameraController.StartCamera(_viewModel.RightCameraAddress);
    }

    public void RightCameraStopped(object? sender, RoutedEventArgs e)
    {
        _rightCameraController.StopCamera(Camera.Right);
        _rightCameraController.StopMjpegStreaming();
    }

    public void RightOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        _rightCameraController.SetTrackingMode();
    }

    public void RightOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        _rightCameraController.SetCroppingMode();
    }

    public void RightSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        _rightCameraController.SelectEntireFrame();
    }

    // Event handlers for face camera
    public void FaceCameraStart(object? sender, RoutedEventArgs e)
    {
        _faceCameraController.StartCamera(_viewModel.FaceCameraAddress);
    }

    public void FaceCameraStopped(object? sender, RoutedEventArgs e)
    {
        _faceCameraController.StopCamera(Camera.Face);
    }

    public void FaceOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        _faceCameraController.SetTrackingMode();
    }

    public void FaceOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        _faceCameraController.SetCroppingMode();
    }

    public void FaceSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        _faceCameraController.SelectEntireFrame();
    }

    private async void OnVRCalibrationRequested(object? sender, RoutedEventArgs e)
    {
        const int leftPort = 8080;
        const int rightPort = 8081;

        if (!OperatingSystem.IsWindows()) return;

        var model = new VRCalibration
        {
            ModelSavePath = Path.GetTempPath(),
            CalibrationInstructions = [11],
            FOV = 1f,
            LeftEyeMjpegSource = $"http://localhost:{leftPort}/",
            RightEyeMjpegSource = $"http://localhost:{rightPort}/",
        };

        _leftCameraController.StartMjpegStreaming(leftPort);
        _rightCameraController.StartMjpegStreaming(rightPort);

        // await _vrService.StartCamerasAsync(model);
    }
}
