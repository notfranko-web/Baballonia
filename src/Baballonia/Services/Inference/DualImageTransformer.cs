using System.Collections.Generic;
using System.Linq;
using Baballonia.Services.Inference.Models;
using OpenCvSharp;

namespace Baballonia.Services.Inference;

public class DualImageTransformer : IImageTransformer
{
    public ImageTransformer LeftTransformer = new();
    public ImageTransformer RightTransformer = new();

    private Queue<Mat> ImageQueue = new();

    public Mat? Apply(Mat image)
    {
        // Assuming the frame is wide enough to be split in half
        var width = image.Width;
        var height = image.Height;

        // Split the frame into left and right halves
        var leftHalf = new Rect(0, 0, width / 2, height);
        var rightHalf = new Rect(width / 2, 0, width / 2, height);

        // Create ROIs for left and right eyes
        using var leftRoi = new Mat(image, leftHalf);
        using var rightRoi = new Mat(image, rightHalf);

        // transform both simultaneously with same settings
        var leftTransformed = LeftTransformer.Apply(leftRoi);
        var rightTransformed =  RightTransformer.Apply(rightRoi);
        if (leftTransformed == null || rightTransformed == null)
        {
            leftTransformed?.Dispose();
            rightTransformed?.Dispose();
            return null;
        }

        var histMatLeft = new Mat();
        var histMatRight = new Mat();
        Cv2.EqualizeHist(leftTransformed, histMatLeft);
        Cv2.EqualizeHist(rightTransformed, histMatRight);

        var combined = new Mat();
        Cv2.Merge([histMatLeft, histMatRight], combined);

        leftTransformed.Dispose();
        rightTransformed.Dispose();
        histMatLeft.Dispose();
        histMatRight.Dispose();

        ImageQueue.Enqueue(combined);

        if (ImageQueue.Count < 5)
            return null;

        var removed = ImageQueue.Dequeue();
        removed.Dispose();

        var last4 = ImageQueue.Skip(ImageQueue.Count - 4).Take(4).Reverse().ToArray();

        var channels = new List<Mat>();
        foreach (var m in last4)
        {
            Mat[] splitChannels = Cv2.Split(m);
            channels.AddRange(splitChannels);
        }
        Mat octoMat = new Mat();
        Cv2.Merge(channels.ToArray(), octoMat);

        foreach (var channel in channels)
            channel.Dispose();

        return octoMat;
    }
}
