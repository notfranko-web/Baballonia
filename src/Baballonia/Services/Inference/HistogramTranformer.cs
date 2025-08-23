using OpenCvSharp;

namespace Baballonia.Services.Inference;

public class HistogramTranformer : IImageTransformer
{
    public Mat? Apply(Mat image)
    {
        Mat result = new Mat();
        Cv2.EqualizeHist(image, result);
        return result;
    }
}
