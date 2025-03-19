using System;
using AvaloniaMiaDev.Services.Camera.Enums;
using AvaloniaMiaDev.Services.Camera.Models;

namespace AvaloniaMiaDev.Contracts;

public interface IInferenceService
{
    public int Fps => (int) MathF.Floor(1000f / Ms);
    public float Ms { get; set; }

    public bool GetExpressionData(CameraSettings cameraSettings, out float[] arKitExpressions);

    public bool GetRawImage(CameraSettings cameraSettings, ColorType color, out byte[] image, out (int width, int height) dimensions);

    public bool GetImage(CameraSettings cameraSettings, out byte[]? image, out (int width, int height) dimensions);

    public void ConfigurePlatformConnectors(Chirality chirality, string cameraIndex);
}
