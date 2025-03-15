using System;
using AvaloniaMiaDev.Services.Camera.Enums;

namespace AvaloniaMiaDev.Contracts;

public interface IInferenceService
{
    public int Fps => (int) MathF.Floor(1000f / Ms);
    public float Ms { get; set; }

    public bool GetExpressionData(Chirality cameraIndex, out float[] arKitExpressions);

    public bool GetRawImage(Chirality cameraIndex, ColorType color, out byte[] image, out (int width, int height) dimensions);

    public bool GetImage(Chirality cameraIndex, out byte[]? image, out (int width, int height) dimensions);

    public void ConfigurePlatformConnectors(Chirality chirality, string cameraIndex);
}
