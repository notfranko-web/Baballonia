using System;
using System.Threading.Tasks;
using AvaloniaMiaDev.Services.Camera.Enums;

namespace AvaloniaMiaDev.Contracts;

public interface IInferenceService
{
    public int FPS => (int) MathF.Floor(1000f / MS);
    public float MS { get; set; }

    public bool GetExpressionData(Chirality cameraIndex, out float[] ARKitExpressions);

    public bool GetRawImage(Chirality cameraIndex, ColorType color, out byte[] image, out (int width, int height) dimensions);

    public bool GetImage(Chirality cameraIndex, out byte[]? image, out (int width, int height) dimensions);

    public void ConfigurePlatformConnectors(Chirality chirality, string cameraIndex);
}
