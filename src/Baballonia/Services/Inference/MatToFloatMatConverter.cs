using OpenCvSharp;

namespace Baballonia.Services.Inference;

public class MatToFloatMatConverter : IImageConverter
{
    public Mat? Convert(Mat input)
    {
        Mat resultMat = new Mat();
        if (input.Type() != MatType.CV_32F)
        {
            input.ConvertTo(resultMat, MatType.CV_32F, 1f / 255f);
            return resultMat;
        }
        return input;
    }
}
