using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.Services;

namespace AvaloniaMiaDev.Helpers
{
    public static class VRCalibrationLoader
    {
        private static readonly VRCalibrationService _service = new VRCalibrationService();
        private static Process _vrProcess;

        public static async Task Start(VRCalibrationModel model)
        {
            try
            {
                // First try to connect to an existing process
                try
                {
                    var status = await _service.GetStatusAsync();
                }
                catch
                {
                    // If we can't connect, start a new process
                    StartVRProcess();

                    // Wait for the process to start up
                    await Task.Delay(3000);
                }

                // Start the cameras
                if (!await _service.StartCamerasAsync())
                {
                    throw new Exception("Failed to start cameras");
                }

                // Start calibration or preview based on model settings
                if (model.IsCalibrationMode)
                {
                    await StartCalibration(model);
                }
                else
                {
                    await StartPreview(model);
                }
            }
            catch (Exception ex)
            {
                // Consider showing an error dialog to the user
            }
        }

        private static void StartVRProcess()
        {
            // Path to the VR calibration executable
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Calibration", "calibrator.exe");

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException("VR calibration executable not found", exePath);
            }

            _vrProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    CreateNoWindow = false
                },
                EnableRaisingEvents = true
            };

            _vrProcess.Exited += (sender, args) =>
            {
                _vrProcess = null;
            };

            _vrProcess.Start();
        }

        private static async Task StartCalibration(VRCalibrationModel model)
        {
            string outputPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "VRCalibration",
                $"calibration_{DateTime.Now:yyyyMMdd_HHmmss}.onnx"
            );

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Start the calibration with the selected routine
            if (!await _service.StartCalibrationAsync(outputPath, model.RoutineId))
            {
                throw new Exception("Failed to start calibration");
            }

            // Optionally, start a monitoring task to track progress
            _ = MonitorCalibrationProgress(outputPath, model);
        }

        private static async Task StartPreview(VRCalibrationModel model)
        {
            if (string.IsNullOrEmpty(model.ModelPath) || !File.Exists(model.ModelPath))
            {
                throw new FileNotFoundException("Model file not found", model.ModelPath);
            }

            if (!await _service.StartPreviewAsync(model.ModelPath))
            {
                throw new Exception("Failed to start preview");
            }
        }

        private static async Task MonitorCalibrationProgress(string outputPath, VRCalibrationModel model)
        {
            bool isComplete = false;

            while (!isComplete)
            {
                try
                {
                    var status = await _service.GetStatusAsync();
                    isComplete = status.IsCalibrationComplete;

                    // Update progress in the model
                    model.Progress = status.Progress;

                    if (isComplete)
                    {
                        model.CompletedModelPath = outputPath;
                        model.IsComplete = true;
                        break;
                    }

                    await Task.Delay(1000); // Check every second
                }
                catch (Exception ex)
                {
                    break;
                }
            }
        }

        public static async Task StopPreview()
        {
            try
            {
                await _service.StopPreviewAsync();
            }
            catch (Exception ex)
            {

            }
        }
    }
}
