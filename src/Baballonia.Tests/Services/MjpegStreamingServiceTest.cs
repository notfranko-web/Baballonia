using System.Threading;
using Baballonia.Services;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCvSharp;

namespace Baballonia.Tests.Services;

[TestClass]
[TestSubject(typeof(MjpegStreamingService))]
public class MjpegStreamingServiceTest
{

    MjpegStreamingService _mjpegStreamingService;

    [TestMethod]
    public void test1()
    {
        _mjpegStreamingService = new MjpegStreamingService();
        _mjpegStreamingService.StartStreaming(8080);
        Mat mat = new Mat(1000, 1000, MatType.CV_8U);
        _mjpegStreamingService.UpdateMjpegFrame(mat);

        Thread.Sleep(100000);

    }
}
