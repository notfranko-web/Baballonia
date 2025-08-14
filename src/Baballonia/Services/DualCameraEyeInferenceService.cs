using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Baballonia.Services.Inference.Enums;
using Baballonia.Services.Inference.Filters;
using Baballonia.Services.Inference.Models;
using Baballonia.Services.Inference.Platforms;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Baballonia.Services;

/// <summary>
/// Implementation of IEyeInferenceService that uses SEPARATE cameras for each eye
/// </summary>
public class DualCameraEyeInferenceService(ILogger<InferenceService> logger, ILocalSettingsService settingsService) : BaseEyeInferenceService(logger, settingsService), IDualCameraEyeInferenceService
{
    private readonly Dictionary<Camera, string> _cameraUrls = new();
    private readonly (PlatformSettings, PlatformConnector)[] _platformConnectors = new (PlatformSettings, PlatformConnector)[3];

    private readonly ILogger<InferenceService> _logger = logger;
    private readonly ILocalSettingsService _settingsService = settingsService;

    public override EyeInferenceType Type => EyeInferenceType.DualCamera;
    public override IReadOnlyDictionary<Camera, string> CameraUrls => _cameraUrls;
    public override (PlatformSettings, PlatformConnector)[] PlatformConnectors => _platformConnectors;

    public override void SetupInference(Camera camera, string cameraAddress)
    {
        Task.Run(async () =>
        {
            _logger.LogInformation($"Setting up {Type} inference for {camera} camera at {cameraAddress}");

            // Store the camera URL
            _cameraUrls[camera] = cameraAddress;

            // If we already have both cameras set up, no need to reinitialize
            if (_cameraUrls.Count < 2)
                return;

            if (camera == Camera.Left)
                await InitializeModel();
        });
    }

    protected override async Task InitializeModel()
    {
        SessionOptions sessionOptions = SetupSessionOptions();
        await ConfigurePlatformSpecificGpu(sessionOptions);

        var minCutoff = await _settingsService.ReadSettingAsync<float>("AppSettings_OneEuroMinFreqCutoff");
        if (minCutoff == 0f) minCutoff = 1f;
        var speedCoeff = await _settingsService.ReadSettingAsync<float>("AppSettings_OneEuroSpeedCutoff");
        if (speedCoeff == 0f) speedCoeff = 1f;
        var eyeModel = await _settingsService.ReadSettingAsync<string>("EyeHome_EyeModel") ?? "eyeModel.onnx";

        var session = new InferenceSession(Path.Combine(AppContext.BaseDirectory, eyeModel), sessionOptions);
        var inputName = session.InputMetadata.Keys.First();
        var dimensions = session.InputMetadata.Values.First().Dimensions;
        var inputSize = new Size(dimensions[2], dimensions[3]);

        // Initialize tensors and filters for both eyes
        for (int i = 0; i < 2; i++)
        {
            float[] noisy_point = new float[ExpectedRawExpressions];
            var filter = new OneEuroFilter(
                x0: noisy_point,
                minCutoff: minCutoff,
                beta: speedCoeff
            );

            var tensor = new DenseTensor<float>([1, 4, dimensions[2], dimensions[3]]);
            var platformSettings = new PlatformSettings(inputSize, session, tensor, filter, 0f, inputName, eyeModel);
            _platformConnectors[i] = (platformSettings, null)!;
        }

        _combinedDimensions = [1, 8, dimensions[2], dimensions[3]]; // 2 eyes * 4 frames
        _combinedTensor = new DenseTensor<float>(_combinedDimensions);

        // Configure platform connectors for both cameras
        ConfigurePlatformConnectors(Camera.Left, _cameraUrls[Camera.Left]);
        ConfigurePlatformConnectors(Camera.Right, _cameraUrls[Camera.Right]);

        _logger.LogInformation($"{Type} inference service initialized with separate cameras");
    }

    public override bool GetExpressionData(CameraSettings cameraSettings, out float[] arKitExpressions)
    {
        arKitExpressions = null!;

        // For dual camera, we only process when we have both camera feeds
        if (PlatformConnectors[(int)Camera.Left].Item2?.Capture?.IsReady != true ||
            PlatformConnectors[(int)Camera.Right].Item2?.Capture?.IsReady != true)
        {
            return false;
        }

        using var leftEyeMat = new Mat<byte>(
            PlatformConnectors[(int)Camera.Left].Item1.InputSize.Height,
            PlatformConnectors[(int)Camera.Left].Item1.InputSize.Width);

        using var rightEyeMat = new Mat<byte>(
            PlatformConnectors[(int)Camera.Right].Item1.InputSize.Height,
            PlatformConnectors[(int)Camera.Right].Item1.InputSize.Width);

        // Capture frame from both cameras
        if (!CaptureFrame(cameraSettings, leftEyeMat, rightEyeMat))
        {
            return false;
        }

        // Check if we have enough frames in the queue
        if (_frameQueues.Count < FramesForInference)
        {
            return false;
        }

        // Pop old frames until we have exactly FramesForInference
        while (_frameQueues.Count > FramesForInference)
        {
            _frameQueues.TryDequeue(out _);
        }

        // Convert queued frames to tensor
        ConvertMatsArrayToDenseTensor(_frameQueues.Select(fd => fd.Mat).ToArray());

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(PlatformConnectors[(int)Camera.Left].Item1.InputName, _combinedTensor)
        };

        using var results = PlatformConnectors[(int)Camera.Left].Item1.Session!.Run(inputs);
        arKitExpressions = results[0].AsEnumerable<float>().ToArray();

        // Apply filter
        arKitExpressions = PlatformConnectors[(int)Camera.Left].Item1.Filter.Filter(arKitExpressions);

        // Process and convert the expressions to the expected format
        return ProcessExpressions(ref arKitExpressions);;
    }

    private bool ProcessExpressions(ref float[] arKitExpressions)
    {
        if (arKitExpressions.Length < ExpectedRawExpressions)
            return false;

        const float MUL_V = 2.0f;
        const float MUL_Y = 2.0f;

        var left_pitch = arKitExpressions[0] * MUL_Y - MUL_Y / 2;
        var left_yaw = arKitExpressions[1] * MUL_V - MUL_V / 2;
        var left_lid = 1 - arKitExpressions[2];

        var right_pitch = arKitExpressions[3] * MUL_Y - MUL_Y / 2;
        var right_yaw = arKitExpressions[4] * MUL_V - MUL_V / 2;
        var right_lid = 1 - arKitExpressions[5];

        var eye_Y = (left_pitch * left_lid + right_pitch * right_lid) / (left_lid + right_lid);

        var left_eye_yaw_corrected = right_yaw * (1 - left_lid) + left_yaw * left_lid;
        var right_eye_yaw_corrected = left_yaw * (1 - right_lid) + right_yaw * right_lid;

        // [left pitch, left yaw, left lid...
        float[] convertedExpressions = new float[ExpectedRawExpressions];

        // swap eyes at this point
        convertedExpressions[0] = right_eye_yaw_corrected; // left pitch
        convertedExpressions[1] = eye_Y;                   // left yaw
        convertedExpressions[2] = right_lid;               // left lid
        convertedExpressions[3] = left_eye_yaw_corrected;  // right pitch
        convertedExpressions[4] = eye_Y;                   // right yaw
        convertedExpressions[5] = left_lid;                // right lid

        arKitExpressions = convertedExpressions;

        float time = (float)sw.Elapsed.TotalSeconds;
        var delta = time - PlatformConnectors[(int)Camera.Left].Item1.LastTime;
        PlatformConnectors[(int)Camera.Left].Item1.Ms = delta * 1000f;
        PlatformConnectors[(int)Camera.Right].Item1.Ms = delta * 1000f;

        PlatformConnectors[(int)Camera.Left].Item1.LastTime = time;
        PlatformConnectors[(int)Camera.Right].Item1.LastTime = time;

        return true;
    }

    public override bool GetRawImage(CameraSettings cameraSettings, ColorType color, out Mat image)
    {
        var index = (int)cameraSettings.Camera;
        var platformConnector = PlatformConnectors[index].Item2;
        image = new Mat();

        if (platformConnector?.Capture?.RawMat == null || !platformConnector.Capture.IsReady)
            return false;

        if (color == (platformConnector.Capture.RawMat.Channels() == 1 ? ColorType.Gray8 : ColorType.Bgr24))
        {
            image = platformConnector.Capture.RawMat;
        }
        else
        {
            var convertedMat = new Mat();
            Cv2.CvtColor(platformConnector.Capture.RawMat, convertedMat,
                platformConnector.Capture.RawMat.Channels() == 1
                    ? color switch
                    {
                        ColorType.Bgr24 => ColorConversionCodes.GRAY2BGR,
                        ColorType.Rgb24 => ColorConversionCodes.GRAY2RGB,
                        ColorType.Rgba32 => ColorConversionCodes.GRAY2RGBA,
                        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
                    }
                    : color switch
                    {
                        ColorType.Gray8 => ColorConversionCodes.BGR2GRAY,
                        ColorType.Rgb24 => ColorConversionCodes.BGR2RGB,
                        ColorType.Rgba32 => ColorConversionCodes.BGR2RGBA,
                        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
                    });
            image = convertedMat;
        }

        return true;
    }

    public override bool GetImage(CameraSettings cameraSettings, out Mat? image)
    {
        image = null;
        var platformSettings = PlatformConnectors[(int)cameraSettings.Camera].Item1;
        var platformConnector = PlatformConnectors[(int)cameraSettings.Camera].Item2;
        if (platformConnector is null)
            return false;

        var imageMat = new Mat<byte>(platformSettings.InputSize.Height, platformSettings.InputSize.Width);

        if (platformConnector.TransformRawImage(imageMat, cameraSettings) != true)
        {
            imageMat.Dispose();
            return false;
        }

        image = imageMat;
        return true;
    }
}
