using System;
using System.IO;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Baballonia.Desktop.Calibration.Aero.Overlay;
using Baballonia.Helpers;
using Baballonia.Models;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Enums;
using Baballonia.ViewModels.SplitViewPane;
using Microsoft.ML.OnnxRuntime;
using Newtonsoft.Json;

namespace Baballonia.Desktop.Calibration.Aero;

public partial class AeroOverlayTrainerCombo
{
    private async Task<bool> StartCamerasAsync(VrCalibration calibration)
    {
        var response = await _httpClient.GetStringAsync(new Uri($"{_baseUrl}/start_cameras?left={calibration.LeftEyeMjpegSource}&right={calibration.RightEyeMjpegSource}"));
        var result = JsonConvert.DeserializeObject<ApiResponse>(response);
        return result!.Result == "ok";
    }

    private async Task<bool> StartCalibrationAsync(VrCalibration calibration)
    {
        var url = $"{_baseUrl}/start_calibration?onnx_filename={VrCalibration.ModelName}&routine_id={calibration.CalibrationInstructions}";
        var response = await _httpClient.GetStringAsync(url);
        var result = JsonConvert.DeserializeObject<ApiResponse>(response);
        return result!.Result == "ok";
    }

    public async Task EyeTrackingCalibrationRequested(string calibrationRoutine, CameraController leftCameraController, CameraController rightCameraController, ILocalSettingsService localSettingsService, IInferenceService eyeInferenceService)
    {
        const int leftPort = 8080;
        const int rightPort = 8081;
        var modelPath = Directory.GetCurrentDirectory();
        var calibration = new VrCalibration
        {
            ModelSavePath = modelPath,
            CalibrationInstructions = calibrationRoutine,
            FOV = 1f,
            LeftEyeMjpegSource = $"http://localhost:{leftPort}/mjpeg",
            RightEyeMjpegSource = $"http://localhost:{rightPort}/mjpeg",
        };

        // Now for the IPC. Spool up our MJPEG streams
        leftCameraController.StartMjpegStreaming(leftPort);
        rightCameraController.StartMjpegStreaming(rightPort);

        // Tell the calibrator/overlay to accept our streams, then start calibration
        await StartOverlay();
        await StartCamerasAsync(calibration);
        await StartCalibrationAsync(calibration);

        // Wait for the process to exit
        var loop = true;
        while (loop)
        {
            var status = await GetStatusAsync();
            if (status.IsTrained)
            {
                loop = false;
            }

            await Task.Delay(1000);
        }

        // Stop the MJPEG streams, we don't need them anymore
        StopOverlay();
        leftCameraController.StopMjpegStreaming();
        rightCameraController.StopMjpegStreaming();

        // Save the location of the model so when we boot up the app it autoloads
        const string modelName = "tuned_temporal_eye_tracking.onnx";
        await localSettingsService.SaveSettingAsync("EyeHome_EyeModel", modelName);

        // Cleanup any leftover capture.bin files
        DeleteCaptureFiles(modelPath);

        SessionOptions sessionOptions = eyeInferenceService.SetupSessionOptions();
        await eyeInferenceService.ConfigurePlatformSpecificGpu(sessionOptions);
    }
}
