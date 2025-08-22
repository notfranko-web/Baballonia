using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Baballonia.Contracts;
using Baballonia.Services.Inference;
using Microsoft.Extensions.Logging;

namespace Baballonia.Services;

public class ProcessingLoopService : IDisposable
{
    public record struct Bitmaps(WriteableBitmap? FaceBitmap, WriteableBitmap? LeftBitmap, WriteableBitmap? RightBitmap);

    public record struct Expressions(float[]? FaceExpression, float[]? EyeExpression);

    public event Action<Expressions> ExpressionChangeEvent;

    public readonly DefaultProcessingPipeline FaceProcessingPipeline = new();

    private readonly ILocalSettingsService _localSettingsService;

    private readonly DispatcherTimer _drawTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(10)
    };

    private readonly ILogger<ProcessingLoopService> _logger;

    public ProcessingLoopService(
        ILogger<ProcessingLoopService> logger,
        ILocalSettingsService localSettingsService,
        IFaceInferenceService faceInferenceService,
        IServiceProvider serviceProvider)
    {
        _localSettingsService = localSettingsService;
        _logger = logger;

        _drawTimer.Tick += TimerEvent;
        _drawTimer.Start();
    }

    private void TimerEvent(object? s, EventArgs e)
    {
        try
        {
            var expressions = new Expressions();

            var faceExpression = FaceProcessingPipeline.RunUpdate();
            if (faceExpression != null)
                expressions.FaceExpression = faceExpression;

            if(expressions.FaceExpression != null || expressions.EyeExpression != null)
                ExpressionChangeEvent?.Invoke(expressions);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected Exception, pausing capture: {}", ex);
            FaceProcessingPipeline.VideoSource?.Stop();
            //_drawTimer.Stop();
        }
    }

    public void Start()
    {
        _drawTimer.Start();
    }

    public void Pause()
    {
        _drawTimer.Stop();
    }

    public void Dispose()
    {
        _drawTimer.Stop();
    }
}
