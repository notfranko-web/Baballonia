using System;
using System.Collections.Generic;
using AvaloniaMiaDev.Services.Inference.Enums;
using AvaloniaMiaDev.Services.Inference.Models;
using AvaloniaMiaDev.Services.Inference.Platforms;

namespace AvaloniaMiaDev.Contracts;

public interface IInferenceService
{
    public bool GetExpressionData(Camera cameraSettings, out float[] arKitExpressions);

    public bool GetRawImage(CameraSettings cameraSettings, ColorType color, out byte[] image, out (int width, int height) dimensions);

    public bool GetImage(CameraSettings cameraSettings, out byte[]? image, out (int width, int height) dimensions);

    public void ConfigurePlatformConnectors(Camera camera, string cameraIndex);

    public void Shutdown(Camera camera);
    public void Shutdown();
}
