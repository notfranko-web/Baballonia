namespace Baballonia.Helpers;

public static class FloatExtensions
{
    public static float Remap (this float from, float toMin,  float toMax)
    {
        var fromAbs  =  from - toMin;
        var fromMaxAbs = toMax - toMin;

        var normal = fromAbs / fromMaxAbs;

        return normal;
    }
}
