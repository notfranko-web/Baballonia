namespace Baballonia.Services.Inference.Models;

public class CameraSettings
{
    public Enums.Camera Camera { get; set; }
    public int RoiX { get; set; } = 0;
    public int RoiY { get; set; } = 0;
    public int RoiWidth { get; set; } = 640;
    public int RoiHeight { get; set; } = 480;
    public float RotationRadians { get; set; } = 0;
    public bool UseRedChannel { get; set; } = false;
    public bool UseHorizontalFlip { get; set; } = false;
    public bool UseVerticalFlip { get; set; } = false;
    public float Brightness { get; set; } = 1f;
}
