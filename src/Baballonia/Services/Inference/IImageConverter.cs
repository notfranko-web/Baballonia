using OpenCvSharp;

namespace Baballonia.Services.Inference;

public interface IImageConverter
{
    Mat? Convert(Mat input);
}
