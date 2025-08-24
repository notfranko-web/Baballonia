using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Services;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Enums;
using Baballonia.Services.Inference.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Buffer = System.Buffer;
using Rect = Avalonia.Rect;

namespace Baballonia.ViewModels.SplitViewPane;

public partial class HomePageViewModel : ViewModelBase, IDisposable
{
    // This feels unorthodox but... i kinda like it?
    public partial class CameraControllerModel : ObservableObject, IDisposable
    {
        public string Name;
        public readonly CropManager CropManager = new();
        public CamViewMode CamViewMode = CamViewMode.Tracking;
        public readonly Camera Camera;

        [ObservableProperty] private WriteableBitmap? _bitmap;

        [ObservableProperty] private bool _startButtonEnabled = true;
        [ObservableProperty] private bool _stopButtonEnabled = false;
        [ObservableProperty] private bool _hintEnabled = false;
        [ObservableProperty] private bool _inferEnabled = false;
        [ObservableProperty] private string _displayAddress;
        [ObservableProperty] private Rect _overlayRectangle;
        [ObservableProperty] private bool _flipHorizontally = false;
        [ObservableProperty] private bool _flipVertically = false;
        [ObservableProperty] private float _rotation = 0f;
        [ObservableProperty] private float _gamma = 1f;
        [ObservableProperty] private bool _isCropMode = false;
        [ObservableProperty] private bool _isCameraRunning = false;
        public ObservableCollection<string> Suggestions { get; set; } = [];

        private readonly ILocalSettingsService _localSettingsService;
        private readonly DefaultProcessingPipeline _processingPipeline;

        public CameraControllerModel(ILocalSettingsService localSettingsService, string name,
            DefaultProcessingPipeline processingPipeline, Camera camera)
        {
            _localSettingsService = localSettingsService;
            _processingPipeline = processingPipeline;
            Name = name;
            Camera = camera;

            _ = InitializeAsync();

            _processingPipeline.TransformedFrameEvent += ImageUpdateEventHandler;
        }

        private async Task InitializeAsync()
        {
            var displayAddress = await _localSettingsService.ReadSettingAsync<string>("LastOpened" + Name);
            var camSettings = await _localSettingsService.ReadSettingAsync<CameraSettings>(Name);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DisplayAddress = displayAddress;
                FlipHorizontally = camSettings.UseHorizontalFlip;
                FlipVertically = camSettings.UseVerticalFlip;
                Rotation = camSettings.RotationRadians;
                Gamma = camSettings.Gamma;


                CropManager.SetCropZone(camSettings.Roi);
                OverlayRectangle = CropManager.CropZone.GetRect();
                OnCropUpdated();

                switch (_processingPipeline.VideoSource)
                {
                    case null:
                        return;
                    case SingleCameraSource singleCamera:
                        StartButtonEnabled = false;
                        StopButtonEnabled = true;
                        break;
                    case DualCameraSource dualCamera:
                        switch (Camera)
                        {
                            case Camera.Left when dualCamera.LeftCam == null:
                            case Camera.Right when dualCamera.RightCam == null:
                                return;
                            default:
                                StartButtonEnabled = false;
                                StopButtonEnabled = true;
                                break;
                        }
                        break;
                }
            });
        }


        public void OnCropUpdated()
        {
            OverlayRectangle = CropManager.CropZone.GetRect();
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.Roi = CropManager.CropZone;
            }
            else if (t is DualImageTransformer dualTransformer)
            {
                if (Camera == Camera.Left)
                    dualTransformer.LeftTransformer.Transformation.Roi = CropManager.CropZone;
                if (Camera == Camera.Right)
                    dualTransformer.RightTransformer.Transformation.Roi = CropManager.CropZone;
            }
            SaveTransformer();
        }

        [RelayCommand]
        public void StopCamera()
        {
            _processingPipeline.VideoSource?.Dispose();
            _processingPipeline.VideoSource = null;

            Bitmap = null;

            IsCameraRunning = false;
            StartButtonEnabled = true;
            StopButtonEnabled = false;
        }

        void ImageUpdateEventHandler(Mat image)
        {
            if (image == null)
            {
                IsCameraRunning = false;
                Bitmap = null;
                return;
            }

            if (!IsCameraRunning)
                return;

            if (Camera == Camera.Face)
            {
                UpdateBitmap(image);
                return;
            }

            int channels = image.Channels();
            if (channels == 1)
            {
                var width = image.Width;
                var height = image.Height;
                switch (Camera)
                {
                    case Camera.Left:
                    {
                        var leftHalf = new OpenCvSharp.Rect(0, 0, width / 2, height);
                        var leftRoi = new Mat(image, leftHalf);
                        UpdateBitmap(leftRoi);
                        break;
                    }
                    case Camera.Right:
                    {
                        var rightHalf = new OpenCvSharp.Rect(width / 2, 0, width / 2, height);
                        var rightRoi = new Mat(image, rightHalf);
                        UpdateBitmap(rightRoi);
                        break;
                    }
                }
            }
            else if (channels == 2)
            {
                var images = image.Split();

                if (Camera == Camera.Left)
                    UpdateBitmap(images[0]);
                else if (Camera == Camera.Right)
                    UpdateBitmap(images[1]);
            }
        }

        void UpdateBitmap(Mat image)
        {
            if (_bitmap is null ||
                _bitmap.PixelSize.Width != image.Width ||
                _bitmap.PixelSize.Height != image.Height)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize(image.Width, image.Height),
                    new Vector(96, 96),
                    image.Channels() == 3 ? PixelFormats.Bgr24 : PixelFormats.Gray8,
                    AlphaFormat.Opaque);
            }

            CropManager.MaxSize.Height = _bitmap.PixelSize.Height;
            CropManager.MaxSize.Width = _bitmap.PixelSize.Width;

            if (!image.IsContinuous()) image = image.Clone();

            // scope for "using" a lock hehe...
            {
                using var frameBuffer = _bitmap.Lock();

                IntPtr srcPtr = image.Data;
                IntPtr destPtr = frameBuffer.Address;
                int size = image.Rows * image.Cols * image.ElemSize();

                unsafe
                {
                    Buffer.MemoryCopy(srcPtr.ToPointer(), destPtr.ToPointer(), size, size);
                }
            }

            IsCameraRunning = true;
            var tmp = Bitmap;
            Bitmap = null;
            Bitmap = tmp;
        }

        partial void OnBitmapChanged(WriteableBitmap? value)
        {
            // IsCameraRunning = value != null;
        }

        partial void OnFlipHorizontallyChanged(bool value)
        {
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.UseHorizontalFlip = value;
            }
            else if (t is DualImageTransformer dualTransformer)
            {
                if (Camera == Camera.Left)
                    dualTransformer.LeftTransformer.Transformation.UseHorizontalFlip = value;
                if (Camera == Camera.Right)
                    dualTransformer.RightTransformer.Transformation.UseHorizontalFlip = value;
            }
            SaveTransformer();
        }

        partial void OnFlipVerticallyChanged(bool value)
        {
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.UseVerticalFlip = value;
            }
            else if (t is DualImageTransformer dualTransformer)
            {
                if (Camera == Camera.Left)
                    dualTransformer.LeftTransformer.Transformation.UseVerticalFlip = value;
                if (Camera == Camera.Right)
                    dualTransformer.RightTransformer.Transformation.UseVerticalFlip = value;
            }
            SaveTransformer();
        }

        partial void OnRotationChanged(float value)
        {
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.RotationRadians = value;
            }
            else if (t is DualImageTransformer dualTransformer)
            {
                if (Camera == Camera.Left)
                    dualTransformer.LeftTransformer.Transformation.RotationRadians = value;
                if (Camera == Camera.Right)
                    dualTransformer.RightTransformer.Transformation.RotationRadians = value;
            }
            SaveTransformer();
        }

        void SaveTransformer()
        {
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                _localSettingsService.SaveSettingAsync(Name, transformer.Transformation);
            }
            else if (t is DualImageTransformer dualTransformer)
            {
                if(Camera == Camera.Left)
                    _localSettingsService.SaveSettingAsync(Name, dualTransformer.LeftTransformer.Transformation);
                if(Camera == Camera.Right)
                    _localSettingsService.SaveSettingAsync(Name, dualTransformer.RightTransformer.Transformation);
            }
        }

        partial void OnGammaChanged(float value)
        {
            // If the slider is close enough to 1, then we treat it as 1
            value = Math.Abs(value - 1) > 0.1f ? value : 1f;

            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.Gamma = value;
            }
            else if (t is DualImageTransformer dualTransformer)
            {
                if (Camera == Camera.Left)
                    dualTransformer.LeftTransformer.Transformation.Gamma = value;
                if (Camera == Camera.Right)
                    dualTransformer.RightTransformer.Transformation.Gamma = value;
            }
            SaveTransformer();
        }

        partial void OnIsCropModeChanged(bool value)
        {
            if (value)
            {
                _processingPipeline.TransformedFrameEvent -= ImageUpdateEventHandler;
                _processingPipeline.NewFrameEvent += ImageUpdateEventHandler;
                CamViewMode = CamViewMode.Cropping;
            }
            else
            {
                _processingPipeline.NewFrameEvent -= ImageUpdateEventHandler;
                _processingPipeline.TransformedFrameEvent += ImageUpdateEventHandler;
                CamViewMode = CamViewMode.Tracking;
            }
        }

        public void SelectWholeFrame()
        {
            CropManager.SelectEntireFrame(Camera);
            OnCropUpdated();
        }

        private bool _disposed = false;

        public void Dispose()
        {
            if (_disposed) return;

            _processingPipeline.TransformedFrameEvent -= ImageUpdateEventHandler;
            _processingPipeline.NewFrameEvent -= ImageUpdateEventHandler;
        }
    }

    private static bool _hasPerformedFirstTimeSetup = false;

    public IOscTarget OscTarget { get; }
    private OscRecvService OscRecvService { get; }
    private OscSendService OscSendService { get; }
    private ILocalSettingsService LocalSettingsService { get; }

    private int _messagesRecvd;
    [ObservableProperty] private string _messagesInPerSecCount;

    private int _messagesSent;
    [ObservableProperty] private string _messagesOutPerSecCount;

    [ObservableProperty] private bool _shouldShowEyeCalibration;
    [ObservableProperty] private string _selectedCalibrationText;

    [ObservableProperty] private CameraControllerModel _leftCamera;
    [ObservableProperty] private CameraControllerModel _rightCamera;
    [ObservableProperty] private CameraControllerModel _faceCamera;

    private readonly DispatcherTimer _msgCounterTimer;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ProcessingLoopService _processingLoopService;


    private ILogger<HomePageViewModel> _logger;

    public HomePageViewModel()
    {
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        OscRecvService = Ioc.Default.GetService<OscRecvService>()!;
        OscSendService = Ioc.Default.GetService<OscSendService>()!;
        LocalSettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _processingLoopService = Ioc.Default.GetService<ProcessingLoopService>()!;
        _logger = Ioc.Default.GetService<ILogger<HomePageViewModel>>()!;
        LocalSettingsService.Load(this);

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

        LeftCamera = new CameraControllerModel(_localSettingsService, "LeftCamera",
            _processingLoopService.EyesProcessingPipeline, Camera.Left);
        RightCamera = new CameraControllerModel(_localSettingsService, "RightCamera",
            _processingLoopService.EyesProcessingPipeline, Camera.Right);
        FaceCamera = new CameraControllerModel(_localSettingsService, "FaceCamera",
            _processingLoopService.FaceProcessingPipeline, Camera.Face);

        _processingLoopService.PipelineExceptionEvent += PipelineExceptionEventHandler;

        Dispatcher.UIThread.Post(async () =>
        {
            await SetupCameraControllers();
        });
    }

    private void PipelineExceptionEventHandler(Exception ex)
    {
        if (_processingLoopService.FaceProcessingPipeline.VideoSource == null)
        {
            FaceCamera.StartButtonEnabled = true;
            FaceCamera.StopButtonEnabled = false;

            FaceCamera.Bitmap = null;
        }
        if (_processingLoopService.EyesProcessingPipeline.VideoSource == null)
        {
            LeftCamera.StartButtonEnabled = true;
            LeftCamera.StopButtonEnabled = false;
            LeftCamera.Bitmap = null;

            RightCamera.StartButtonEnabled = true;
            RightCamera.StopButtonEnabled = false;
            RightCamera.Bitmap = null;
        }

    }

    private async Task SetupCameraControllers()
    {
        Task.Run(async () =>
        {
            App.DeviceEnumerator.Cameras = await App.DeviceEnumerator.UpdateCameras();
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var camerasKey in App.DeviceEnumerator.Cameras.Keys)
                {
                    LeftCamera.Suggestions.Add(camerasKey);
                    RightCamera.Suggestions.Add(camerasKey);
                    FaceCamera.Suggestions.Add(camerasKey);
                }
            });
        });
    }

    private void SaveCameraSettings()
    {
    }

    private async Task<IVideoSource?> StartCameraAsync(string address)
    {
        var camera = address;
        if (string.IsNullOrEmpty(camera)) return null;

        if (App.DeviceEnumerator.Cameras.TryGetValue(camera, out var mappedAddress))
        {
            camera = mappedAddress;
        }

        return await Task.Run<IVideoSource?>(() =>
        {
            var cameraSource = new SingleCameraSourceFactory().Create(camera);
            if (cameraSource == null)
                return null;

            return !cameraSource.Start() ? null : cameraSource;
        });
    }

    [RelayCommand]
    public void StopCamera(CameraControllerModel model)
    {
        var pipeline = _processingLoopService.EyesProcessingPipeline;
        switch (pipeline.VideoSource)
        {
            case SingleCameraSource singleCameraSource:
                singleCameraSource.Dispose();
                pipeline.VideoSource = null;

                LeftCamera.IsCameraRunning = false;
                LeftCamera.StartButtonEnabled = true;
                LeftCamera.StopButtonEnabled = false;

                RightCamera.IsCameraRunning = false;
                RightCamera.StartButtonEnabled = true;
                RightCamera.StopButtonEnabled = false;
                break;
            case DualCameraSource dualCameraSource:
                if (model.Camera == Camera.Right)
                {
                    dualCameraSource.RightCam?.Dispose();
                    dualCameraSource.RightCam = null;

                    RightCamera.IsCameraRunning = false;
                    RightCamera.StartButtonEnabled = true;
                    RightCamera.StopButtonEnabled = false;
                }
                else if (model.Camera == Camera.Left)
                {
                    dualCameraSource.LeftCam?.Dispose();
                    dualCameraSource.LeftCam = null;

                    LeftCamera.IsCameraRunning = false;
                    LeftCamera.StartButtonEnabled = true;
                    LeftCamera.StopButtonEnabled = false;
                }
                break;
        }
    }

    [RelayCommand]
    public async Task StartFaceCamera()
    {
        FaceCamera.StartButtonEnabled = false;
        FaceCamera.StopButtonEnabled = false;
        var cameraSource = await StartCameraAsync(FaceCamera.DisplayAddress);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cameraSource == null)
            {
                FaceCamera.StartButtonEnabled = true;
                FaceCamera.StopButtonEnabled = false;
                return;
            }

            if (_processingLoopService.FaceProcessingPipeline.VideoSource != null)
            {
                _processingLoopService.FaceProcessingPipeline.VideoSource.Dispose();
                _processingLoopService.FaceProcessingPipeline.VideoSource = null;
            }

            _processingLoopService.FaceProcessingPipeline.VideoSource = cameraSource;

            FaceCamera.IsCameraRunning = true;
            FaceCamera.StartButtonEnabled = false;
            FaceCamera.StopButtonEnabled = true;
        });
        await _localSettingsService.SaveSettingAsync("LastOpened" + FaceCamera.Name, FaceCamera.DisplayAddress);
    }

    [RelayCommand]
    public async Task StartLeftCamera()
    {
        LeftCamera.StartButtonEnabled = false;

        if (!string.IsNullOrEmpty(LeftCamera.DisplayAddress) && string.Equals(RightCamera.DisplayAddress, LeftCamera.DisplayAddress))
        {
            if (_processingLoopService.EyesProcessingPipeline.VideoSource is DualCameraSource dualCameraSource)
            {
                var tmp = dualCameraSource.RightCam;
                _processingLoopService.EyesProcessingPipeline.VideoSource = tmp;

                LeftCamera.IsCameraRunning = true;
                LeftCamera.StopButtonEnabled = true;
                LeftCamera.StartButtonEnabled = false;
                return;
            }
        }

        var cameraSource = await StartCameraAsync(LeftCamera.DisplayAddress);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cameraSource == null)
            {
                LeftCamera.StartButtonEnabled = true;
                LeftCamera.StopButtonEnabled = false;
                return;
            }

            var pipeline = _processingLoopService.EyesProcessingPipeline;

            if (pipeline.VideoSource == null)
            {
                var dualSource = new DualCameraSource();
                dualSource.LeftCam = cameraSource;
                pipeline.VideoSource = dualSource;
            }
            else if (pipeline.VideoSource is DualCameraSource dualSource)
            {
                dualSource.LeftCam = cameraSource;
            }
            else if (pipeline.VideoSource is SingleCameraSource singleCameraSource)
            {
                var tmp = singleCameraSource;
                pipeline.VideoSource = null;
                var dual = new DualCameraSource();
                dual.RightCam = tmp;
                dual.LeftCam = cameraSource;

                pipeline.VideoSource = dual;
            }


            LeftCamera.IsCameraRunning = true;

            LeftCamera.StopButtonEnabled = true;
            LeftCamera.StartButtonEnabled = false;
        });
        await _localSettingsService.SaveSettingAsync("LastOpened" + LeftCamera.Name, LeftCamera.DisplayAddress);
    }

    [RelayCommand]
    public async Task StartRightCamera()
    {
        RightCamera.StartButtonEnabled = false;

        if (!string.IsNullOrEmpty(RightCamera.DisplayAddress) && string.Equals(RightCamera.DisplayAddress, LeftCamera.DisplayAddress))
        {
            if (_processingLoopService.EyesProcessingPipeline.VideoSource is DualCameraSource dualCameraSource)
            {
                var tmp = dualCameraSource.LeftCam;
                _processingLoopService.EyesProcessingPipeline.VideoSource = tmp;

                RightCamera.IsCameraRunning = true;
                RightCamera.StopButtonEnabled = true;
                RightCamera.StartButtonEnabled = false;
                return;
            }
        }

        var cameraSource = await StartCameraAsync(RightCamera.DisplayAddress);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cameraSource == null)
            {
                RightCamera.StartButtonEnabled = true;
                RightCamera.StopButtonEnabled = false;
                return;
            }

            var pipeline = _processingLoopService.EyesProcessingPipeline;

            if (pipeline.VideoSource == null)
            {
                var dualSource = new DualCameraSource();
                dualSource.RightCam = cameraSource;
                pipeline.VideoSource = dualSource;
            }
            else if (pipeline.VideoSource is DualCameraSource dualSource)
            {
                dualSource.RightCam = cameraSource;
            }
            else if (pipeline.VideoSource is SingleCameraSource singleCameraSource)
            {
                var tmp = singleCameraSource;
                pipeline.VideoSource = null;
                var dual = new DualCameraSource();
                dual.LeftCam = tmp;
                dual.RightCam = cameraSource;

                pipeline.VideoSource = dual;
            }

            RightCamera.IsCameraRunning = true;

            RightCamera.StopButtonEnabled = true;
            RightCamera.StartButtonEnabled = false;
        });
        await _localSettingsService.SaveSettingAsync("LastOpened" + RightCamera.Name, RightCamera.DisplayAddress);
    }

    [RelayCommand]
    private void SelectWholeFrame(CameraControllerModel model)
    {
        model.SelectWholeFrame();
    }

    [RelayCommand]
    private async Task RequestVRCalibration()
    {
         await App.Overlay.EyeTrackingCalibrationRequested(CalibrationRoutine.QuickCalibration);
         await _localSettingsService.SaveSettingAsync("EyeHome_EyeModel", "tuned_temporal_eye_tracking.onnx");

         await _processingLoopService.SetupEyeInference();

         // This will restart the right camera, as well as the left
         // CameraStop(LeftCamera);
         // CameraStart(LeftCamera);
    }

    private void MessageDispatched(int msgCount) => _messagesSent += msgCount;

    public void Dispose()
    {
        CleanupResources();
    }

    private bool _disposed = false;

    private void CleanupResources()
    {
        if (_disposed) return;
        FaceCamera.CamViewMode = CamViewMode.Tracking;
        LeftCamera.CamViewMode = CamViewMode.Tracking;
        RightCamera.CamViewMode = CamViewMode.Tracking;

        _faceCamera.Dispose();
        _leftCamera.Dispose();
        _rightCamera.Dispose();

        _processingLoopService.PipelineExceptionEvent -= PipelineExceptionEventHandler;
        OscSendService.OnMessagesDispatched -= MessageDispatched;
        _msgCounterTimer.Stop();
    }
}
