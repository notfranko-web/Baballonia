using System.Reflection;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFaceTracking.Babble;

public class BabbleVrc : ExtTrackingModule
{
    private BabbleOsc babbleOSC;

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        Config babbleConfig = BabbleConfig.GetBabbleConfig();
        babbleOSC = new BabbleOsc(Logger, babbleConfig.Host, babbleConfig.Port);
        List<Stream> list = new List<Stream>();
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        Stream manifestResourceStream = executingAssembly.GetManifestResourceStream("VRCFaceTracking.Babble.BabbleLogo.png")!;
        list.Add(manifestResourceStream);
        ModuleInformation = new ModuleMetadata
        {
            Name = "Project Babble Eye and Face Module v1.0.3",
            StaticImages = list
        };


        return babbleConfig.EnabledFeatrures;
    }

    public override void Teardown()
    {
        babbleOSC.Teardown();
    }

    public override void Update()
    {
        UnifiedTracking.Data.Eye.Left.Gaze.x = BabbleOsc.EyeExpressions[(int)ExpressionMapping.LeftEyeX];
        UnifiedTracking.Data.Eye.Left.Gaze.y = BabbleOsc.EyeExpressions[(int)ExpressionMapping.LeftEyeY];
        UnifiedTracking.Data.Eye.Left.Openness = BabbleOsc.EyeExpressions[(int)ExpressionMapping.LeftEyeLid];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideLeft].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeWiden];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintLeft].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeSquint];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererLeft].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.BrowAngry];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.BrowRaise];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.BrowRaise];

        UnifiedTracking.Data.Eye.Right.Gaze.x = BabbleOsc.EyeExpressions[(int)ExpressionMapping.RightEyeX];
        UnifiedTracking.Data.Eye.Right.Gaze.y = BabbleOsc.EyeExpressions[(int)ExpressionMapping.RightEyeY];
        UnifiedTracking.Data.Eye.Right.Openness = BabbleOsc.EyeExpressions[(int)ExpressionMapping.RightEyeLid];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeWideRight].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeWiden];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.EyeSquintRight].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeSquint];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowLowererRight].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.BrowAngry];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowInnerUpRight].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.BrowRaise];
        UnifiedTracking.Data.Shapes[(int)UnifiedExpressions.BrowOuterUpRight].Weight = BabbleOsc.EyeExpressions[(int)ExpressionMapping.BrowRaise];

        UpdateEye(ref UnifiedTracking.Data.Eye.Left);
        UpdateEye(ref UnifiedTracking.Data.Eye.Right);

        foreach (UnifiedExpressions expression in BabbleExpressions.BabbleExpressionMap!)
        {
            UnifiedTracking.Data.Shapes[(int)expression].Weight = BabbleExpressions.BabbleExpressionMap.GetByKey1(expression);
        }

        Thread.Sleep(10);
    }

    private void UpdateEye(ref UnifiedSingleEyeData eye)
    {
        eye.PupilDiameter_MM = 0.0035f * BabbleOsc.EyeExpressions[(int)ExpressionMapping.EyeDilate];
    }
}
