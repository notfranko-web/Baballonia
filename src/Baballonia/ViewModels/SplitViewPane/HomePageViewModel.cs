using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Discord;
using HarfBuzzSharp;
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

        [ObservableProperty] private WriteableBitmap? _bitmap;

        [ObservableProperty] private bool _hintEnabled;
        [ObservableProperty] private bool _inferEnabled;
        [ObservableProperty] private string _displayAddress = "";
        [ObservableProperty] private Rect _overlayRectangle;
        [ObservableProperty] private bool _flipHorizontally = false;
        [ObservableProperty] private bool _flipVertically = false;
        [ObservableProperty] private float _rotation = 0;
        [ObservableProperty] private bool _isCropMode = false;
        [ObservableProperty] private bool _isCameraRunning = false;
        public ObservableCollection<string> Suggestions { get; set; } = [];

        private readonly ILocalSettingsService _localSettingsService;
        private readonly DefaultProcessingPipeline _processingPipeline;

        public CameraControllerModel(ILocalSettingsService localSettingsService, string name,
            DefaultProcessingPipeline processingPipeline)
        {
            _localSettingsService = localSettingsService;
            _processingPipeline = processingPipeline;
            Name = name;

            Dispatcher.UIThread.Post(async () =>
            {
                DisplayAddress = await _localSettingsService.ReadSettingAsync<string>("LastOpened" + Name);
                var camSettings = await _localSettingsService.ReadSettingAsync<CameraSettings>(name);
                CropManager.SetCropZone(camSettings.Roi);
                OverlayRectangle = CropManager.CropZone.GetRect();
            });

            if(processingPipeline != null)
                _processingPipeline.TransformedFrameEvent += ImageUpdateEventHandler;
        }

        public void OnCropUpdated()
        {
            OverlayRectangle = CropManager.CropZone.GetRect();
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.Roi = CropManager.CropZone;
                SaveTransformer(transformer.Transformation);
            }
        }

        [RelayCommand]
        private void StartCamera()
        {
            var camera = DisplayAddress;

            if (string.IsNullOrEmpty(camera)) return;

            if (App.DeviceEnumerator.Cameras.TryGetValue(camera, out var mappedAddress))
            {
                camera = mappedAddress;
            }

            var cameraSource = new SingleCameraSourceFactory().Create(camera);
            if (cameraSource == null)
                return;

            cameraSource.Start();
            _processingPipeline.VideoSource = cameraSource;
            var imageT = new ImageTransformer();
            imageT.Transformation.Roi = CropManager.CropZone;
            _processingPipeline.ImageTransformer = imageT;
            if (_processingPipeline.InferenceService == null)
            {
                var inferenceRunner =
                    new DefaultInferenceRunner(Ioc.Default.GetService<ILogger<DefaultInferenceRunner>>()!);
                inferenceRunner.Setup("faceModel.onnx", false);
                _processingPipeline.InferenceService = inferenceRunner;
            }

            _processingPipeline.ImageConverter = new MatToFloatTensorConverter();
        }

        [RelayCommand]
        void StopCamera()
        {
            _processingPipeline.VideoSource?.Stop();
        }

        void ImageUpdateEventHandler(Mat image)
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
            IsCameraRunning = value != null;
        }

        partial void OnDisplayAddressChanged(string value)
        {
            _localSettingsService.SaveSettingAsync("LastOpened" + Name, value);
        }

        partial void OnFlipHorizontallyChanged(bool value)
        {
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.UseHorizontalFlip = value;
                SaveTransformer(transformer.Transformation);
            }
        }

        partial void OnFlipVerticallyChanged(bool value)
        {
            var t = _processingPipeline.ImageTransformer;
            if (t is ImageTransformer transformer)
            {
                transformer.Transformation.UseVerticalFlip = value;
                SaveTransformer(transformer.Transformation);
            }
        }

        partial void OnRotationChanged(float value)
        {
            if (_processingPipeline.ImageTransformer is ImageTransformer transformer)
            {
                transformer.Transformation.RotationRadians = value;
                SaveTransformer(transformer.Transformation);
            }
        }
        void SaveTransformer(CameraSettings transformer)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await _localSettingsService.SaveSettingAsync(Name, transformer);
            });
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
            CropManager.SelectEntireFrame(Camera.Face);
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

    [ObservableProperty] private bool _shouldShowEyeCalibration;
    [ObservableProperty] private string _selectedCalibrationText;

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
    private IInferenceService _eyeInferenceService;
    private readonly IFaceInferenceService _faceInferenceService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ProcessingLoopService _processingLoopService;


    private ILogger<HomePageViewModel> _logger;

    public HomePageViewModel()
    {
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        OscRecvService = Ioc.Default.GetService<OscRecvService>()!;
        OscSendService = Ioc.Default.GetService<OscSendService>()!;
        LocalSettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _serviceProvider = Ioc.Default.GetRequiredService<IServiceProvider>();
        _faceInferenceService = Ioc.Default.GetService<IFaceInferenceService>()!;
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

        LeftCamera = new CameraControllerModel(_localSettingsService, "LeftCamera", null);
        RightCamera = new CameraControllerModel(_localSettingsService, "RightCamera", null);
        FaceCamera = new CameraControllerModel(_localSettingsService, "FaceCamera",
            _processingLoopService.FaceProcessingPipeline);

        Dispatcher.UIThread.Post(async () => { await SetupCameraControllers(); });
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
        // _localSettingsService.SaveSettingAsync("LeftCamera",
        //     _processingLoopService.LeftCameraController.CameraSettings);
        // _localSettingsService.SaveSettingAsync("RightCamera",
        //     _processingLoopService.RightCameraController.CameraSettings);
        // _localSettingsService.SaveSettingAsync("FaceCamera",
        //     _processingLoopService.FaceCameraController.CameraSettings);
    }

    [RelayCommand]
    private async void CameraStart(CameraControllerModel model)
    {
        // model.Controller.StartCamera(camera);
        //
        // if (model.Name == "FaceCamera") return;

        // if (_eyeInferenceService is DualCameraEyeInferenceService)
        // {
        //     switch (model.Controller.CameraSettings.Camera)
        //     {
        //         case Camera.Left:
        //         {
        //             if (LeftCamera.DisplayAddress != RightCamera.DisplayAddress)
        //             {
        //                 if (!string.IsNullOrEmpty(RightCamera.DisplayAddress))
        //                 {
        //                     if (App.DeviceEnumerator.Cameras!.TryGetValue(RightCamera.DisplayAddress, out var mappedAddress))
        //                     {
        //                         // _processingLoopService.RightCameraController.StartCamera(mappedAddress);
        //                     }
        //                 }
        //             }
        //
        //             break;
        //         }
        //         case Camera.Right:
        //         {
        //             if (LeftCamera.DisplayAddress != RightCamera.DisplayAddress)
        //             {
        //                 if (!string.IsNullOrEmpty(LeftCamera.DisplayAddress))
        //                 {
        //                     if (App.DeviceEnumerator.Cameras!.TryGetValue(LeftCamera.DisplayAddress, out var mappedAddress))
        //                     {
        //                         // _processingLoopService.LeftCameraController.StartCamera(mappedAddress);
        //                     }
        //                 }
        //             }
        //
        //             break;
        //         }
        //     }
        // }

        SaveCameraSettings();
    }

    [RelayCommand]
    private void SelectWholeFrame(CameraControllerModel model)
    {
        model.SelectWholeFrame();
    }

    [RelayCommand]
    private async Task RequestVRCalibration()
    {
        // await App.Overlay.EyeTrackingCalibrationRequested(CalibrationRoutine.QuickCalibration,
        //     _processingLoopService.LeftCameraController, _processingLoopService.RightCameraController,
        //     _localSettingsService, _eyeInferenceService);
        // await _localSettingsService.SaveSettingAsync("EyeHome_EyeModel", "tuned_temporal_eye_tracking.onnx");

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

        _faceCamera.Dispose();
        _leftCamera.Dispose();
        _rightCamera.Dispose();

        OscSendService.OnMessagesDispatched -= MessageDispatched;
        _msgCounterTimer.Stop();

        // _processingLoopService.BitmapUpdateEvent -= BitmapUpdateHandler;
    }
}
