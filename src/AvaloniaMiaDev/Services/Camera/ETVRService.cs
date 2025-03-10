using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Services.Camera.Enums;
using AvaloniaMiaDev.Services.Camera.Filters;
using AvaloniaMiaDev.Services.Camera.Platforms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace AvaloniaMiaDev.Services;

public class ETVRService(ILogger<ETVRService> logger, ILocalSettingsService settingsService, int cameraID)
{
    public bool IsRunning { get; private set; }
    public float MS { get; private set; }
    public int FPS => (int)MathF.Floor(1000f / MS);

    public PlatformConnector? PlatformConnector;

    private readonly DenseTensor<float> _inputTensor = new DenseTensor<float>([1, 1, 256, 256]);
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    private Size _inputSize = new Size(256, 256);
    private InferenceSession? _session;
    private OneEuroFilter? _floatFilter;
    private float _lastTime = 0;
    private string? _inputName;

    private const string CameraKey = "CameraIndex";

    public async Task ExecuteAsync()
    {
        logger.LogInformation($"Starting ETVRService {cameraID}...");

        SessionOptions sessionOptions = SetupSessionOptions();
        ConfigurePlatformSpecificGPU(sessionOptions);

        await ConfigurePlatformConnector();

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

        logger.LogInformation($"ETVRService {cameraID} started!");
    }

    /// <summary>
    /// Poll expression data, frames
    /// </summary>
    /// <param name="ARKitExpressions"></param>
    /// <returns></returns>
    public bool GetExpressionData(out float[] ARKitExpressions)
    {
        ARKitExpressions = null;
        if (!IsRunning)
        {
            return false;
        }

        // Test if the camera is not ready or connecting to new source
        if (!PlatformConnector.ExtractFrameData(_inputTensor.Buffer.Span, _inputSize).Result) return false;

        // Camera ready, prepare Mat as DenseTensor
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, _inputTensor)
        };

        // Run inference!
        using var results = _session.Run(inputs);
        var output = results[0].AsEnumerable<float>().ToArray();
        float time = (float)_sw.Elapsed.TotalSeconds;
        MS = (time - _lastTime) * 1000;

        // Filter ARKit Expressions
        for (int i = 0; i < output.Length; i++)
        {
            output[i] = _floatFilter.Filter(output[i], time - _lastTime);
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
    public bool GetRawImage(ColorType color, out byte[] image, out (int width, int height) dimensions)
    {
        if (PlatformConnector?.Capture!.RawMat is null)
        {
            dimensions = (0, 0);
            image = Array.Empty<byte>();
            return false;
        }

        dimensions = PlatformConnector.Capture.Dimensions;
        if (color == ((PlatformConnector.Capture.RawMat.Channels() == 1) ? ColorType.GRAY_8 : ColorType.BGR_24))
        {
            image = PlatformConnector.Capture.RawMat.AsSpan<byte>().ToArray();
        }
        else
        {
            using var convertedMat = new Mat();
            Cv2.CvtColor(PlatformConnector.Capture.RawMat, convertedMat, (PlatformConnector.Capture.RawMat.Channels() == 1) ? color switch
            {
                ColorType.BGR_24 => ColorConversionCodes.GRAY2BGR,
                ColorType.RGB_24 => ColorConversionCodes.GRAY2RGB,
                ColorType.RGBA_32 => ColorConversionCodes.GRAY2RGBA,
            } : color switch
            {
                ColorType.GRAY_8 => ColorConversionCodes.BGR2GRAY,
                ColorType.RGB_24 => ColorConversionCodes.BGR2RGB,
                ColorType.RGBA_32 => ColorConversionCodes.BGR2RGBA,
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
    public unsafe bool GetImage(out byte[]? image, out (int width, int height) dimensions)
    {
        image = null;
        dimensions = (0, 0);

        byte[] data = new byte[_inputSize.Width * _inputSize.Height];
        using var imageMat = Mat<byte>.FromPixelData(_inputSize.Height, _inputSize.Width, data);
        if (PlatformConnector?.TransformRawImage(imageMat).Result != true) return false;

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
    private async Task ConfigurePlatformConnector()
    {
        var index = await settingsService.ReadSettingAsync<string>(CameraKey);

        // We should never change from Android to Desktop or vice versa during
        // the applications runtime. Only run these once!
        if (PlatformConnector is null)
        {
            if (OperatingSystem.IsAndroid())
            {
                PlatformConnector = new AndroidConnector(index, logger, settingsService);
            }
            else
            {
                // Else, for WinUI, macOS, watchOS, MacCatalyst, tvOS, Tizen, etc...
                // Use the standard EmguCV VideoCapture backend
                PlatformConnector = new DesktopConnector(index, logger, settingsService);
            }
        }

        PlatformConnector.Initialize(index);
    }

    /// <summary>
    /// Per-platform hardware accel. detection/activation
    /// </summary>
    /// <param name="sessionOptions"></param>
    private void ConfigurePlatformSpecificGPU(SessionOptions sessionOptions)
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
            logger.LogInformation("Initialized ExecutionProvider: nnAPI");
            return;
        }

        if (OperatingSystem.IsIOS() ||
            OperatingSystem.IsMacCatalyst() ||
            OperatingSystem.IsMacOS() ||
            OperatingSystem.IsWatchOS() ||
            OperatingSystem.IsTvOS())
        {
            sessionOptions.AppendExecutionProvider_CoreML();
            logger.LogInformation("Initialized ExecutionProvider: CoreML");
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            // If DirectML is supported on the user's system, try using it first.
            // This has support for both AMD and Nvidia GPUs, and uses less memory in my testing
            try
            {
                sessionOptions.AppendExecutionProvider_DML();
                logger.LogInformation("Initialized ExecutionProvider: DirectML");
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to configure Gpu.");
                logger.LogWarning("Failed to create DML Execution Provider on Windows. Falling back to CUDA...");
            }
        }


        // If the user's system does not support DirectML (for whatever reason,
        // it's shipped with Windows 10, version 1903(10.0; Build 18362)+
        // Fallback on good ol' CUDA
        try
        {
            sessionOptions.AppendExecutionProvider_CUDA();
            logger.LogInformation("Initialized ExecutionProvider: CUDA");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure Gpu.");
            logger.LogWarning("Failed to create CUDA Execution Provider on Windows.");
        }

        logger.LogWarning("No GPU acceleration will be applied.");
    }
}
