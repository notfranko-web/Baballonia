using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Baballonia.Calibration;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Models;
using Baballonia.Services;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Enums;
using Baballonia.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.ML.OnnxRuntime;

namespace Baballonia.Views;

public partial class HomePageView : UserControl
{
    public static bool ForceInference;

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

    private CameraController LeftCameraController { get; set; }
    private CameraController RightCameraController { get; set; }
    private CameraController FaceCameraController { get; set; }

    private readonly IEyeInferenceService _eyeInferenceService;
    private readonly IFaceInferenceService _faceInferenceService;
    private readonly IVrService _vrService;
    private readonly HomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private readonly DispatcherTimer _drawTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(10)
    };

    public HomePageView()
    {
        InitializeComponent();

        if (!(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()))
        {
            SizeChanged += (_, _) =>
            {
                var window = this.GetVisualRoot() as Window;
                if (window != null)
                {
                    var uniformGrid = this.FindControl<UniformGrid>("UniformGridPanel");
                    if (window.ClientSize.Width < Utils.MobileWidth)
                    {
                        uniformGrid!.Columns = 1; // Vertical layout
                        uniformGrid.Rows = 3;
                    }
                    else
                    {
                        uniformGrid!.Columns = 3; // Horizontal layout
                        uniformGrid.Rows = 1;
                    }
                }
            };
        }

        Loaded += CamView_OnLoaded;

        _viewModel = Ioc.Default.GetRequiredService<HomePageViewModel>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _eyeInferenceService = Ioc.Default.GetService<IEyeInferenceService>()!;
        _faceInferenceService = Ioc.Default.GetService<IFaceInferenceService>()!;
        _vrService = Ioc.Default.GetService<IVrService>()!;
        _localSettingsService.Load(this);

        try
        {
            var cameraDevices = DeviceEnumerator.ListCameras();

            var cameraEntries = cameraDevices.Keys;
            LeftCameraAddressEntry.ItemsSource = cameraEntries;
            RightCameraAddressEntry.ItemsSource = cameraEntries;
            FaceCameraAddressEntry.ItemsSource = cameraEntries;

            // Set MinimumPrefixLength to 0 to show all items even when no text is entered
            // Set MinimumPopulateDelay to 0 to show the dropdown immediately
            LeftCameraAddressEntry.MinimumPrefixLength = 0;
            LeftCameraAddressEntry.MinimumPopulateDelay = TimeSpan.Zero;

            RightCameraAddressEntry.MinimumPrefixLength = 0;
            RightCameraAddressEntry.MinimumPopulateDelay = TimeSpan.Zero;

            FaceCameraAddressEntry.MinimumPrefixLength = 0;
            FaceCameraAddressEntry.MinimumPopulateDelay = TimeSpan.Zero;
        }
        catch (Exception)
        {
            // Insufficient perms, ignore
        }

        LeftCameraController = new CameraController(
            this,
            _localSettingsService,
            _eyeInferenceService,
            Camera.Left,
            LeftRectangleWindow,
            LeftSelectEntireFrame,
            LeftViewBox,
            LeftMouthWindow,
            LeftCanvasWindow,
            "EyeHome_LeftCameraIndex",
            "EyeHome_LeftCameraROIX",
            "EyeHome_LeftCameraROIY",
            "EyeHome_LeftCameraROIWidth",
            "EyeHome_LeftCameraROIHeight",
            "EyeHome_LeftEyeRotation",
            "EyeHome_FlipLeftEyeXAxis",
            "EyeHome_FlipLeftEyeYAxis",
            IsLeftTrackingModeProperty);

        RightCameraController = new CameraController(
            this,
            _localSettingsService,
            _eyeInferenceService,
            Camera.Right,
            RightRectangleWindow,
            RightSelectEntireFrame,
            RightViewBox,
            RightMouthWindow,
            RightCanvasWindow,
            "EyeHome_RightCameraIndex",
            "EyeHome_RightCameraROIX",
            "EyeHome_RightCameraROIY",
            "EyeHome_RightCameraROIWidth",
            "EyeHome_RightCameraROIHeight",
            "EyeHome_RightEyeRotation",
            "EyeHome_FlipRightEyeXAxis",
            "EyeHome_FlipRightEyeYAxis",
            IsRightTrackingModeProperty);

        FaceCameraController = new CameraController(
            this,
            _localSettingsService,
            _faceInferenceService,
            Camera.Face,
            FaceRectangleWindow,
            FaceSelectEntireFrame,
            FaceViewBox,
            FaceMouthWindow,
            FaceCanvasWindow,
            "EyeHome_FaceCameraIndex",
            "Face_CameraROIX",
            "Face_CameraROIY",
            "Face_CameraROIWidth",
            "Face_CameraROIHeight",
            "Face_Rotation",
            "Face_FlipXAxis",
            "Face_FlipYAxis",
            IsFaceTrackingModeProperty);

        LeftCameraStart(null, null!);
        RightCameraStart(null, null!);
        FaceCameraStart(null, null!);

        _drawTimer.Stop();
        _drawTimer.Tick += async (s, e) =>
        {
            await LeftCameraController.UpdateImage();
            await RightCameraController.UpdateImage();
            await FaceCameraController.UpdateImage();

            _viewModel.LeftEyeBitmap = LeftCameraController.Bitmap;
            _viewModel.RightEyeBitmap = RightCameraController.Bitmap;
            _viewModel.FaceBitmap = FaceCameraController.Bitmap;
        };
        _drawTimer.Start();

        var parameterSenderService = Ioc.Default.GetService<ParameterSenderService>()!;
        parameterSenderService.RegisterLeftCameraController(LeftCameraController!);
        parameterSenderService.RegisterRightCameraController(RightCameraController!);
        parameterSenderService.RegisterFaceCameraController(FaceCameraController!);

        PropertyChanged += (_, _) => { _localSettingsService.Save(this); };
    }

    private void CamView_OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateAddressHint(LeftCameraAddressEntry, LeftAddressHint);
        UpdateAddressHint(RightCameraAddressEntry, RightAddressHint);
        UpdateAddressHint(FaceCameraAddressEntry, FaceAddressHint);
    }

    // Event handlers for left camera
    public void LeftCameraStart(object? sender, RoutedEventArgs e)
    {
        LeftCameraController.StopCamera();
        string selectedFriendlyName = LeftCameraAddressEntry.Text!;
        string cameraAddress = selectedFriendlyName;

        // If the friendly name exists in our dictionary, use the corresponding device ID
        if (DeviceEnumerator.Cameras.TryGetValue(selectedFriendlyName, out var deviceId))
        {
            cameraAddress = deviceId;
        }

        LeftCameraController.StartCamera(cameraAddress);
    }

    public void LeftCameraStop(object? sender, RoutedEventArgs e)
    {
        LeftCameraController.StopMjpegStreaming();
        LeftCameraController.StopCamera();
    }

    public void LeftOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        LeftCameraController.SetTrackingMode();
    }

    public void LeftOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        LeftCameraController.SetCroppingMode();
    }

    public async void LeftSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        await LeftCameraController.SelectEntireFrame();
    }

    // Event handlers for right camera
    public void RightCameraStart(object? sender, RoutedEventArgs e)
    {
        RightCameraController.StopCamera();
        string selectedFriendlyName = RightCameraAddressEntry.Text!;
        string cameraAddress = selectedFriendlyName;

        // If the friendly name exists in our dictionary, use the corresponding device ID
        if (DeviceEnumerator.Cameras.TryGetValue(selectedFriendlyName, out var deviceId))
        {
            cameraAddress = deviceId;
        }

        RightCameraController.StartCamera(cameraAddress);
    }

    public void RightCameraStop(object? sender, RoutedEventArgs e)
    {
        RightCameraController.StopMjpegStreaming();
        RightCameraController.StopCamera();
    }

    public void RightOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        RightCameraController.SetTrackingMode();
    }

    public void RightOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        RightCameraController.SetCroppingMode();
    }

    public async void RightSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        await RightCameraController.SelectEntireFrame();
    }

    // Event handlers for face camera
    public void FaceCameraStart(object? sender, RoutedEventArgs e)
    {
        FaceCameraController.StopCamera();
        string selectedFriendlyName = FaceCameraAddressEntry.Text!;
        string cameraAddress = selectedFriendlyName;

        // If the friendly name exists in our dictionary, use the corresponding device ID
        if (DeviceEnumerator.Cameras.TryGetValue(selectedFriendlyName, out var deviceId))
        {
            cameraAddress = deviceId;
        }

        FaceCameraController.StartCamera(cameraAddress);
    }

    public void FaceCameraStop(object? sender, RoutedEventArgs e)
    {
        FaceCameraController.StopMjpegStreaming();
        FaceCameraController.StopCamera();
    }

    public void FaceOnTrackingModeClicked(object sender, RoutedEventArgs args)
    {
        FaceCameraController.SetTrackingMode();
    }

    public void FaceOnCroppingModeClicked(object sender, RoutedEventArgs args)
    {
        FaceCameraController.SetCroppingMode();
    }

    public async void FaceSelectEntireFrameClicked(object sender, RoutedEventArgs args)
    {
        await FaceCameraController.SelectEntireFrame();
    }

    private async void OnVRCalibrationRequested(object? sender, RoutedEventArgs e)
    {
        const int leftPort = 8080;
        const int rightPort = 8081;
        var modelPath = Directory.GetCurrentDirectory();

        var vrCalibration = new VrCalibration
        {
            ModelSavePath = modelPath,
            CalibrationInstructions = CalibrationRoutine.HorizontalSweep,
            FOV = 1f,
            LeftEyeMjpegSource = $"http://localhost:{leftPort}/mjpeg",
            RightEyeMjpegSource = $"http://localhost:{rightPort}/mjpeg",
        };

        // Now for the IPC. Spool up our MJPEG streams
        LeftCameraController.StartMjpegStreaming(leftPort);
        RightCameraController.StartMjpegStreaming(rightPort);

        // First tell the subprocess to accept our streams, then start calibration
        await _vrService.StartOverlay();
        await _vrService.StartCamerasAsync(vrCalibration);
        await _vrService.StartCalibrationAsync(vrCalibration);

        // Wait for the process to exit
        var loop = true;
        while (loop)
        {
            var status = await _vrService.GetStatusAsync();
            if (status.IsTrained)
            {
                loop = false;
            }

            await Task.Delay(1000);
        }

        // Stop the MJPEG streams, we don't need them anymore
        LeftCameraController.StopMjpegStreaming();
        RightCameraController.StopMjpegStreaming();
        _vrService.StopOverlay();

        // Save the location of the model so when we boot up the app it autoloads
        const string modelName = "tuned_temporal_eye_tracking.onnx";
        await _localSettingsService.SaveSettingAsync("EyeHome_EyeModel", modelName);

        // Cleanup any leftover capture.bin files
        DeleteCaptureFiles(modelPath);

        SessionOptions sessionOptions = _eyeInferenceService.SetupSessionOptions();
        await _eyeInferenceService.ConfigurePlatformSpecificGpu(sessionOptions);

        // Finally, close any open eye cameras. The inference service will spin these up
        LeftCameraController.StopCamera();
        RightCameraController.StopCamera();
        _eyeInferenceService.SetupInference(Camera.Left, _viewModel.LeftCameraAddress);
        _eyeInferenceService.SetupInference(Camera.Right,_viewModel.RightCameraAddress);
    }

    private void CameraAddressEntry_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not AutoCompleteBox entry) return;

        // Determine which hint TextBlock corresponds to the sender
        TextBlock? hintBlock = entry.Name switch
        {
            nameof(LeftCameraAddressEntry) => LeftAddressHint,
            nameof(RightCameraAddressEntry) => RightAddressHint,
            nameof(FaceCameraAddressEntry) => FaceAddressHint,
            _ => null
        };

        if (hintBlock != null)
        {
            UpdateAddressHint(entry, hintBlock);
        }
    }

    private void UpdateAddressHint(AutoCompleteBox entry, TextBlock hint)
    {
        string? address = entry.Text;
        bool showHint = false;

        if (!string.IsNullOrWhiteSpace(address))
        {
            // Basic check: Does it contain ".local" but not "http://"?
            if (address.EndsWith(".local", StringComparison.OrdinalIgnoreCase) &&
                !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                showHint = true;
                hint.Text = "Address might need to start with 'http://' (e.g., http://openiristracker.local/)";
            }
        }

        hint.IsVisible = showHint;
    }

    public static void DeleteCaptureFiles(string directoryPath)
    {
        // Validate directory exists
        if (!Directory.Exists(directoryPath))
            return;

        // Get all files matching the capture pattern
        string[] filesToDelete = Directory.GetFiles(directoryPath, "capture.bin");

        // Delete each file
        foreach (string file in filesToDelete)
        {
            File.Delete(file);
        }
    }
}
