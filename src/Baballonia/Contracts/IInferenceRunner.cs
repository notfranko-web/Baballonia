using OpenCvSharp;

namespace Baballonia.Contracts;

public interface IInferenceRunner
{
    public float[]? Run(Mat image);
}
