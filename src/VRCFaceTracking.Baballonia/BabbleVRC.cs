using Microsoft.Extensions.Logging;
using System.Reflection;

namespace VRCFaceTracking.Baballonia;

public class BabbleVrc : ExtTrackingModule
{
    private BabbleOsc babbleOSC;
    private Config config;

    // We need to call GetBabbleConfig ahead of Initialize
    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        config = BabbleConfig.GetBabbleConfig();
        babbleOSC = new BabbleOsc(Logger, config.Host, config.Port);

        List<Stream> list = new List<Stream>();
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        if (config.IsEyeSupported)
        {
            Logger.LogInformation("Baballonia will use Eye Tracking.");
            Stream manifestResourceStream = executingAssembly.GetManifestResourceStream("VRCFaceTracking.Baballonia.BabbleEyeLogo.png")!;
            list.Add(manifestResourceStream);
        }
        if (config.IsFaceSupported)
        {
            Logger.LogInformation("Baballonia will use Face Tracking.");
            Stream manifestResourceStream = executingAssembly.GetManifestResourceStream("VRCFaceTracking.Baballonia.BabbleFaceLogo.png")!;
            list.Add(manifestResourceStream);
        }

        executingAssembly.GetManifestResourceNames();

        ModuleInformation = new ModuleMetadata
        {
            Name = "Project Babble Module v3.0.0",
            StaticImages = list
        };

        return (config.IsEyeSupported, config.IsFaceSupported);
    }

    public override void Teardown()
    {
        babbleOSC.Teardown();
    }

    public override void Update()
    {
        if (config.IsEyeSupported)
        {
            UnifiedTracking.Data.Eye.Left.Gaze.x = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeLeftX];
            UnifiedTracking.Data.Eye.Left.Gaze.y = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeLeftY];
            UnifiedTracking.Data.Eye.Left.Openness = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeLeftLid];

            UnifiedTracking.Data.Eye.Right.Gaze.x = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeRightX];
            UnifiedTracking.Data.Eye.Right.Gaze.y = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeRightY];
            UnifiedTracking.Data.Eye.Right.Openness = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeRightLid];
        }


        if (config.IsFaceSupported)
        {
            foreach (var expression in BabbleExpressions.BabbleExpressionMap!)
            {
                UnifiedTracking.Data.Shapes[(int)expression].Weight = BabbleExpressions.BabbleExpressionMap.GetByKey1(expression);
            }
        }

        Thread.Sleep(10);
    }
}
