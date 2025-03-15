using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Services.Camera.Enums;
using AvaloniaMiaDev.Services.Camera.Filters;
using AvaloniaMiaDev.Services.Camera.Platforms;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace AvaloniaMiaDev.Services;

public class InferenceService : IInferenceService
{
    public PlatformConnector[] PlatformConnectors = new PlatformConnector[2];
    public int Fps => (int) MathF.Floor(1000f / Ms);
    public float Ms { get; set; }
    public bool IsRunning { get; private set; }

    private readonly DenseTensor<float> _inputTensor = new DenseTensor<float>([1, 1, 256, 256]);
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly ILogger<InferenceService> _logger;
    private readonly ILocalSettingsService _localSettingsService;

    private Size _inputSize = new Size(256, 256);
    private InferenceSession? _session;
    private OneEuroFilter? _floatFilter;
    private float _lastTime = 0;
    private string? _inputName;

    public InferenceService(ILogger<InferenceService> logger, ILocalSettingsService settingsService)
    {
        _logger = logger;
        _localSettingsService = settingsService;

        Task.Run(async () =>
        {
            logger.LogInformation("Starting Inference Service...");

            SessionOptions sessionOptions = SetupSessionOptions();
            ConfigurePlatformSpecificGpu(sessionOptions);

            var minCutoff = await settingsService.ReadSettingAsync<float>("TrackingSettings_OneEuroMinFreqCutoff");
            var speedCoeff = await settingsService.ReadSettingAsync<float>("TrackingSettings_OneEuroSpeedCutoff");
            _floatFilter = new OneEuroFilter(
                minCutoff: minCutoff,
                beta: speedCoeff
            );

            _session = new InferenceSession("model.onnx", sessionOptions);
            _inputName = _session.InputMetadata.Keys.First();
            int[] dimensions = _session.InputMetadata.Values.First().Dimensions;
            _inputSize = new(dimensions[2], dimensions[3]);
            IsRunning = true;

            logger.LogInformation("Inference started!");
        });
    }

    /// <summary>
    /// Poll expression data, frames
    /// </summary>
    /// <param name="arKitExpressions"></param>
    /// <returns></returns>
    public bool GetExpressionData(Chirality cameraIndex, out float[] arKitExpressions)
    {
        arKitExpressions = null!;
        if (!IsRunning)
        {
            return false;
        }

        if (PlatformConnectors[(int)cameraIndex] is null)
        {
            return false;
        }

        // Test if the camera is not ready or connecting to new source
        if (!PlatformConnectors[(int)cameraIndex].ExtractFrameData(_inputTensor.Buffer.Span, _inputSize).Result) return false;

        // Camera ready, prepare Mat as DenseTensor
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, _inputTensor)
        };

        // Run inference!
        using var results = _session!.Run(inputs);
        arKitExpressions = results[0].AsEnumerable<float>().ToArray();
        float time = (float)_sw.Elapsed.TotalSeconds;
        Ms = (time - _lastTime) * 1000;

        // Filter ARKit Expressions
        for (int i = 0; i < arKitExpressions.Length; i++)
        {
            arKitExpressions[i] = _floatFilter!.Filter(arKitExpressions[i], time - _lastTime);
        }

        _lastTime = time;
        return true;
    }

    /// <summary>
    /// Gets the pre-transform lip image for this frame
    /// This image will be (dimensions.width)px * (dimensions.height)px in provided ColorType
    /// </summary>
    /// <param name="image"></param>
    /// <param name="dimensions"></param>
    /// <returns></returns>
    public bool GetRawImage(Chirality cameraIndex, ColorType color, out byte[] image, out (int width, int height) dimensions)
    {
        if (PlatformConnectors is null)
        {
            dimensions = (0, 0);
            image = Array.Empty<byte>();
            return false;
        }

        if (PlatformConnectors[(int)cameraIndex]?.Capture!.RawMat is null)
        {
            dimensions = (0, 0);
            image = Array.Empty<byte>();
            return false;
        }

        dimensions = PlatformConnectors[(int)cameraIndex].Capture!.Dimensions;
        if (color == ((PlatformConnectors[(int)cameraIndex].Capture!.RawMat.Channels() == 1) ? ColorType.Gray8 : ColorType.Bgr24))
        {
            image = PlatformConnectors[(int)cameraIndex].Capture!.RawMat.AsSpan<byte>().ToArray();
        }
        else
        {
            using var convertedMat = new Mat();
            Cv2.CvtColor(PlatformConnectors[(int)cameraIndex].Capture!.RawMat, convertedMat, (PlatformConnectors[(int)cameraIndex].Capture!.RawMat.Channels() == 1) ? color switch
            {
                ColorType.Bgr24 => ColorConversionCodes.GRAY2BGR,
                ColorType.Rgb24 => ColorConversionCodes.GRAY2RGB,
                ColorType.Rgba32 => ColorConversionCodes.GRAY2RGBA,
            } : color switch
            {
                ColorType.Gray8 => ColorConversionCodes.BGR2GRAY,
                ColorType.Rgb24 => ColorConversionCodes.BGR2RGB,
                ColorType.Rgba32 => ColorConversionCodes.BGR2RGBA,
            });
            image = convertedMat.AsSpan<byte>().ToArray();
        }

        return true;
    }

    /// <summary>
    /// Gets the prost-transform lip image for this frame
    /// This image will be 256*256px, single-channel
    /// </summary>
    /// <param name="image"></param>
    /// <param name="dimensions"></param>
    /// <returns></returns>
    public bool GetImage(Chirality cameraIndex, out byte[]? image, out (int width, int height) dimensions)
    {
        image = null;
        dimensions = (0, 0);
        if (PlatformConnectors is null) return false;

        byte[] data = new byte[_inputSize.Width * _inputSize.Height];
        using var imageMat = Mat<byte>.FromPixelData(_inputSize.Height, _inputSize.Width, data);
        if (PlatformConnectors[(int)cameraIndex]?.TransformRawImage(imageMat).Result != true) return false;

        image = data;
        dimensions = (imageMat.Width, imageMat.Height);
        return true;
    }

    /// <summary>
    /// Make our SessionOptions *fancy*
    /// </summary>
    /// <returns></returns>
    private SessionOptions SetupSessionOptions()
    {
        // Random environment variable(s) to speed up webcam opening on the MSMF backend.
        // https://github.com/opencv/opencv/issues/17687
        Environment.SetEnvironmentVariable("OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS", "0");
        Environment.SetEnvironmentVariable("OMP_NUM_THREADS", "1");

        // Setup inference backend
        var sessionOptions = new SessionOptions();
        sessionOptions.InterOpNumThreads = 1;
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        // ~3% savings worth ~6ms avg latency. Not noticeable at 60fps?
        sessionOptions.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        sessionOptions.EnableMemoryPattern = true;
        return sessionOptions;
    }

    /// <summary>
    /// Creates the proper video streaming classes based on the platform we're deploying to.
    /// EmguCV doesn't have support for VideoCapture on Android, iOS, or UWP
    /// We have a custom implementations for IP Cameras, the de-facto use case on mobile
    /// As well as SerialCameras (not tested on mobile yet)
    /// </summary>
    public void ConfigurePlatformConnectors(Chirality chirality, string cameraIndex)
    {
        if (OperatingSystem.IsAndroid())
        {
            PlatformConnectors[(int)chirality] = new AndroidConnector(cameraIndex, _logger, _localSettingsService);
            PlatformConnectors[(int)chirality].Initialize(cameraIndex);
        }
        else
        {
            // Else, for WinUI, macOS, watchOS, MacCatalyst, tvOS, Tizen, etc...
            // Use the standard EmguCV VideoCapture backend
            PlatformConnectors[(int)chirality] = new DesktopConnector(cameraIndex, _logger, _localSettingsService);
            PlatformConnectors[(int)chirality].Initialize(cameraIndex);
        }
    }

    /// <summary>
    /// Per-platform hardware accel. detection/activation
    /// </summary>
    /// <param name="sessionOptions"></param>
    private void ConfigurePlatformSpecificGpu(SessionOptions sessionOptions)
    {
        sessionOptions.AppendExecutionProvider_CPU();

        // "The Android Neural Networks API (NNAPI) is an Android C API designed for
        // running computationally intensive operations for machine learning on Android devices."
        // It was added in Android 8.1 and will be deprecated in Android 15
        if (OperatingSystem.IsAndroid() &&
            OperatingSystem.IsAndroidVersionAtLeast(8, 1) && // At least 8.1
            !OperatingSystem.IsAndroidVersionAtLeast(15))          // At most 15
        {
            sessionOptions.AppendExecutionProvider_Nnapi();
            _logger.LogInformation("Initialized ExecutionProvider: nnAPI");
            return;
        }

        if (OperatingSystem.IsIOS() ||
            OperatingSystem.IsMacCatalyst() ||
            OperatingSystem.IsMacOS() ||
            OperatingSystem.IsWatchOS() ||
            OperatingSystem.IsTvOS())
        {
            sessionOptions.AppendExecutionProvider_CoreML();
            _logger.LogInformation("Initialized ExecutionProvider: CoreML");
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            // If DirectML is supported on the user's system, try using it first.
            // This has support for both AMD and Nvidia GPUs, and uses less memory in my testing
            try
            {
                sessionOptions.AppendExecutionProvider_DML();
                _logger.LogInformation("Initialized ExecutionProvider: DirectML");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure Gpu.");
                _logger.LogWarning("Failed to create DML Execution Provider on Windows. Falling back to CUDA...");
            }
        }


        // If the user's system does not support DirectML (for whatever reason,
        // it's shipped with Windows 10, version 1903(10.0; Build 18362)+
        // Fallback on good ol' CUDA
        try
        {
            sessionOptions.AppendExecutionProvider_CUDA();
            _logger.LogInformation("Initialized ExecutionProvider: CUDA");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Gpu.");
            _logger.LogWarning("Failed to create CUDA Execution Provider on Windows.");
        }

        _logger.LogWarning("No GPU acceleration will be applied.");
    }
}
