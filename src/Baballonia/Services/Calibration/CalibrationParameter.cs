namespace Baballonia.Services.Calibration;

public class CalibrationParameter
{
    public float Lower { get; set; } = 0f;
    public float Upper { get; set; } = 1f;
    public float Min { get; set; } = 0f;
    public float Max { get; set; } = 1f;

    public CalibrationParameter(float lower = 0f, float upper = 1f, float min = 0f, float max = 1f)
    {
        Lower = lower;
        Upper = upper;
        Min = min;
        Max = max;
    }
}
