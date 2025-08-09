using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Baballonia.Contracts;
using Baballonia.Services;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Enums;
using Baballonia.Services.Inference.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;

namespace Baballonia.ViewModels.SplitViewPane;

public partial class HomePageViewModel : ViewModelBase
{

    // This feels unorthodox but... i kinda like it?
    public partial class CameraControllerModel : ObservableObject
    {
        public string Name;
        public CameraController Controller { get; set; }
        [ObservableProperty] private WriteableBitmap _bitmap;

        [ObservableProperty] private string _displayAddress;
        [ObservableProperty] private Rect _overlayRectangle;
        [ObservableProperty] private bool _flipHorizontally = false;
        [ObservableProperty] private bool _flipVertically = false;
        [ObservableProperty] private float _rotation = 0;
        [ObservableProperty] private bool _isCropMode = false;
        [ObservableProperty] private string _hint;
        [ObservableProperty] private bool _isCameraStarted = false;
        public ObservableCollection<string> Suggestions { get; set; } = [];

        private readonly ILocalSettingsService _localSettingsService;

        public CameraControllerModel(ILocalSettingsService localSettingsService, string name)
        {
            _localSettingsService = localSettingsService;
            Name = name;

            Dispatcher.UIThread.Post(async () =>
            {
                DisplayAddress = await _localSettingsService.ReadSettingAsync<string>("LastOpened" + Name);
            });
        }

        void SaveCameraConfig()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _localSettingsService.SaveSettingAsync(Name, Controller.CameraSettings);
            });
        }

        partial void OnBitmapChanged(WriteableBitmap? value)
        {
            IsCameraStarted = value != null;
        }

        partial void OnOverlayRectangleChanged(Rect value)
        {
            SaveCameraConfig();
        }

        partial void OnDisplayAddressChanged(string value)
        {
            _localSettingsService.SaveSettingAsync("LastOpened" + Name, value);
            SaveCameraConfig();
        }

        partial void OnFlipHorizontallyChanged(bool value)
        {
            Controller.CameraSettings.UseHorizontalFlip = value;
            SaveCameraConfig();
        }

        partial void OnFlipVerticallyChanged(bool value)
        {
            Controller.CameraSettings.UseVerticalFlip = value;
            SaveCameraConfig();
        }

        partial void OnRotationChanged(float value)
        {
            Controller.CameraSettings.RotationRadians = value;
            SaveCameraConfig();
        }

        partial void OnIsCropModeChanged(bool value)
        {
            if (value)
                Controller.SetCroppingMode();
            else
                Controller.SetTrackingMode();
        }

        public void SelectWholeFrame()
        {
            Controller.CropManager.SelectEntireFrame(Controller.CameraSettings.Camera);
            OverlayRectangle = Controller.CropManager.CropZone;
            SaveCameraConfig();
        }
    }

    [ObservableProperty] public bool _shouldShowEyeCalibration;
    [ObservableProperty] public string _selectedCalibrationText;

    [ObservableProperty] [property: SavedSetting("EyeHome_EyeModel", "eyeModel.onnx")]
    private string _eyeModel;

    public IOscTarget OscTarget { get; }
    private OscRecvService OscRecvService { get; }
    private OscSendService OscSendService { get; }
    private ILocalSettingsService LocalSettingsService { get; }

    private int _messagesRecvd;
    [ObservableProperty] private string _messagesInPerSecCount;

    private int _messagesSent;
    [ObservableProperty] private string _messagesOutPerSecCount;

    [ObservableProperty] private CameraControllerModel _leftCamera;
    [ObservableProperty] private CameraControllerModel _rightCamera;
    [ObservableProperty] private CameraControllerModel _faceCamera;

    private readonly DispatcherTimer _msgCounterTimer;
    private readonly IEyeInferenceService _eyeInferenceService;
    private readonly IFaceInferenceService _faceInferenceService;
    private readonly ILocalSettingsService _localSettingsService;


    private readonly DispatcherTimer _drawTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(10)
    };

    public HomePageViewModel()
    {
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        OscRecvService = Ioc.Default.GetService<OscRecvService>()!;
        OscSendService = Ioc.Default.GetService<OscSendService>()!;
        LocalSettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _eyeInferenceService = Ioc.Default.GetService<IEyeInferenceService>()!;
        _faceInferenceService = Ioc.Default.GetService<IFaceInferenceService>()!;
        LocalSettingsService.Load(this);

        ShouldShowEyeCalibration = OperatingSystem.IsWindows() || OperatingSystem.IsLinux();
        MessagesInPerSecCount = "0";
        MessagesOutPerSecCount = "0";
        OscSendService.OnMessagesDispatched += MessageDispatched;

        _msgCounterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _msgCounterTimer.Tick += (_, _) =>
        {
            MessagesInPerSecCount = _messagesRecvd.ToString();
            _messagesRecvd = 0;

            MessagesOutPerSecCount = _messagesSent.ToString();
            _messagesSent = 0;
        };
        _msgCounterTimer.Start();

        LeftCamera = new CameraControllerModel(_localSettingsService, "LeftCamera");
        RightCamera = new CameraControllerModel(_localSettingsService, "RightCamera");
        FaceCamera = new CameraControllerModel(_localSettingsService, "FaceCamera");

        Dispatcher.UIThread.Post(async () => { await SetupCameraControllers(); });
    }

    private CameraController LeftCameraController { get; set; }
    private CameraController RightCameraController { get; set; }
    private CameraController FaceCameraController { get; set; }

    private async Task SetupCameraControllers()
    {
        Task.Run(async () =>
        {
            App.DeviceEnumerator.Cameras = await App.DeviceEnumerator.UpdateCameras();
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var camerasKey in App.DeviceEnumerator.Cameras.Keys)
                    LeftCamera.Suggestions.Add(camerasKey);
            });
        });

        var left = await _localSettingsService.ReadSettingAsync<CameraSettings>("LeftCamera",
            new CameraSettings { Camera = Camera.Left });
        var right = await _localSettingsService.ReadSettingAsync<CameraSettings>("LeftCamera",
            new CameraSettings { Camera = Camera.Left });
        var face = await _localSettingsService.ReadSettingAsync<CameraSettings>("LeftCamera",
            new CameraSettings { Camera = Camera.Left });

        LeftCameraController = new CameraController(
            _localSettingsService,
            _eyeInferenceService,
            Camera.Left,
            left
        );

        RightCameraController = new CameraController(
            _localSettingsService,
            _eyeInferenceService,
            Camera.Right,
            right
        );

        FaceCameraController = new CameraController(
            _localSettingsService,
            _faceInferenceService,
            Camera.Face,
            face
        );

        LeftCamera.Controller = LeftCameraController;
        RightCamera.Controller = RightCameraController;
        FaceCamera.Controller = FaceCameraController;

        _drawTimer.Stop();
        _drawTimer.Tick += async (s, e) =>
        {
            await LeftCameraController.UpdateImage(bitmap =>
            {
                // a hack to force the UI refresh
                LeftCamera.Bitmap = null;
                LeftCamera.Bitmap = bitmap;
            });
            await RightCameraController.UpdateImage(bitmap =>
            {
                RightCamera.Bitmap = null;
                RightCamera.Bitmap = bitmap;
            });
            await FaceCameraController.UpdateImage(bitmap =>
            {
                FaceCamera.Bitmap = null;
                FaceCamera.Bitmap = bitmap;
            });
        };
        _drawTimer.Start();

        var parameterSenderService = Ioc.Default.GetService<ParameterSenderService>()!;
        parameterSenderService.RegisterLeftCameraController(LeftCameraController!);
        parameterSenderService.RegisterRightCameraController(RightCameraController!);
        parameterSenderService.RegisterFaceCameraController(FaceCameraController!);
    }

    private void SaveCameraSettings()
    {
        _localSettingsService.SaveSettingAsync("LeftCamera", LeftCameraController.CameraSettings);
        _localSettingsService.SaveSettingAsync("RightCamera", RightCameraController.CameraSettings);
        _localSettingsService.SaveSettingAsync("FaceCamera", FaceCameraController.CameraSettings);
    }

    [RelayCommand]
    private void CameraStart(CameraControllerModel model)
    {
        model.Controller.StartCamera(model.DisplayAddress);
        SaveCameraSettings();
    }

    [RelayCommand]
    private void CameraStop(CameraControllerModel model)
    {
        model.Controller.StopCamera();
    }

    [RelayCommand]
    private void SelectWholeFrame(CameraControllerModel model)
    {
        model.SelectWholeFrame();
    }

private void MessageDispatched(int msgCount) => _messagesSent += msgCount;

    ~HomePageViewModel()
    {
        CleanupResources();
    }

    private void CleanupResources()
    {
        OscSendService.OnMessagesDispatched -= MessageDispatched;
        _msgCounterTimer.Stop();
    }
}
