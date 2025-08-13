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
/// Implementation of IEyeInferenceService that uses a SINGLE camera for both eyes
/// </summary>
public class SingleCameraEyeInferenceService(ILogger<InferenceService> logger, ILocalSettingsService settingsService) : BaseEyeInferenceService(logger, settingsService), ISingleCameraEyeInferenceService
{
    private readonly Dictionary<Camera, string> _cameraUrls = new();
    private readonly (PlatformSettings, PlatformConnector)[] _platformConnectors = new (PlatformSettings, PlatformConnector)[3];
    private string _cameraAddress;

    private readonly ILogger<InferenceService> _logger = logger;
    private readonly ILocalSettingsService _settingsService = settingsService;

    public override EyeInferenceType Type => EyeInferenceType.SingleCamera;
    public override IReadOnlyDictionary<Camera, string> CameraUrls => _cameraUrls;
    public override (PlatformSettings, PlatformConnector)[] PlatformConnectors => _platformConnectors;

    public override void SetupInference(Camera camera, string cameraAddress)
    {
        Task.Run(async () =>
        {
            _logger.LogInformation($"Setting up {Type} inference with camera at {cameraAddress}");

            // Store the camera URL for both eyes
            _cameraUrls[Camera.Left] = cameraAddress;
            _cameraUrls[Camera.Right] = cameraAddress;
            _cameraAddress = cameraAddress;

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

        // Initialize tensor and filter for the single camera
        float[] noisy_point = new float[ExpectedRawExpressions];
        var filter = new OneEuroFilter(
            x0: noisy_point,
            minCutoff: minCutoff,
            beta: speedCoeff
        );

        var tensor = new DenseTensor<float>([1, 4, dimensions[2], dimensions[3]]);
        var platformSettings = new PlatformSettings(inputSize, session, tensor, filter, 0f, inputName, eyeModel);

        // Use the same platform settings and connector for both eyes
        _platformConnectors[(int)Camera.Left] = (platformSettings, null)!;
        _platformConnectors[(int)Camera.Right] = (platformSettings, null)!;

        _combinedDimensions = [1, 8, dimensions[2], dimensions[3]]; // 2 eyes * 4 frames
        _combinedTensor = new DenseTensor<float>(_combinedDimensions);

        // Configure platform connector for the single camera
        ConfigurePlatformConnectors(Camera.Left, _cameraAddress);

        // For single camera, we'll use the same connector for both eyes
        _platformConnectors[(int)Camera.Right] = _platformConnectors[(int)Camera.Left];

        _logger.LogInformation($"{Type} inference service initialized with single camera");
    }

    public override bool GetExpressionData(CameraSettings cameraSettings, out float[] arKitExpressions)
    {
        arKitExpressions = null!;

        if (PlatformConnectors[(int)Camera.Left].Item2?.Capture?.IsReady != true)
        {
            return false;
        }

        // For single camera, we'll split the frame vertically for left/right eyes
        using var frame = new Mat<byte>(
            PlatformConnectors[(int)Camera.Left].Item1.InputSize.Height,
            PlatformConnectors[(int)Camera.Left].Item1.InputSize.Width);

        // Get the full frame from the camera
        var platformConnector = PlatformConnectors[(int)Camera.Left].Item2;
        if (platformConnector.TransformRawImage(frame, cameraSettings) != true)
        {
            return false;
        }

        // Split the frame vertically for left and right eyes
        using var leftEyeMat = new Mat();
        using var rightEyeMat = new Mat();

        // Assuming the frame is wide enough to be split in half
        var width = frame.Width;
        var height = frame.Height;

        // Split the frame into left and right halves
        var leftHalf = new Rect(0, 0, width / 2, height);
        var rightHalf = new Rect(width / 2, 0, width / 2, height);

        // Create ROIs for left and right eyes
        using var leftRoi = new Mat(frame, leftHalf);
        using var rightRoi = new Mat(frame, rightHalf);

        // Resize ROIs to the expected input size
        Cv2.Resize(leftRoi, leftEyeMat, new Size(
            PlatformConnectors[(int)Camera.Left].Item1.InputSize.Width,
            PlatformConnectors[(int)Camera.Left].Item1.InputSize.Height));

        Cv2.Resize(rightRoi, rightEyeMat, new Size(
            PlatformConnectors[(int)Camera.Right].Item1.InputSize.Width,
            PlatformConnectors[(int)Camera.Right].Item1.InputSize.Height));

        // Capture frame using the split eye images
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

        // Hacky
        var res = ProcessExpressions(ref arKitExpressions);
        ParameterSenderService.EyeExpressions = arKitExpressions;

        // Process and convert the expressions to the expected format
        return res;
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
        var platformConnector = PlatformConnectors[(int)Camera.Left].Item2;
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
        var platformSettings = PlatformConnectors[(int)Camera.Left].Item1;
        var platformConnector = PlatformConnectors[(int)Camera.Left].Item2;
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
