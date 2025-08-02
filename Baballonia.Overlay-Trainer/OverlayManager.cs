using System.Numerics;
using StereoKit;

public class OverlayManager
{
    private Pose SpherePose;

    private MjpegStreamCapture? _leftEyeStream;
    private MjpegStreamCapture? _rightEyeStream;
    private GazeCapture? _gazeCapture;
    private TrainingDataWriter? _dataWriter;
    private List<TrainingSample> _samples = new();
    private bool _isGazeTraining = false;
    private DateTime _gazeStartTime;
    private TimeSpan _gazeDuration = TimeSpan.FromMinutes(3);
    private System.Action<float>? _progressCallback;

    public void Initialize()
    {
        Task.Run(() =>
        {
            SK.Initialize();
            SK.Run(() =>
            {
                var point = GetPointInFrontOfUser(2);
                Mesh.Sphere.Draw(Material.Default, Matrix.TRS(point, Quat.Identity, 0.1f));
            });
        });
    }


    public void StartGazeTraining(System.Action<float> onProgress)
    {
        _isGazeTraining = true;
        _gazeStartTime = DateTime.UtcNow;
        _progressCallback = onProgress;
        _samples.Clear();
        _dataWriter = new TrainingDataWriter("training_data.bin");
        _gazeCapture = new GazeCapture(GetHeadPosition, GetHeadRotation, GetSpherePosition);
        _leftEyeStream = new MjpegStreamCapture("http://localhost:8080", mat => OnEyeFrame(mat, true));
        _rightEyeStream = new MjpegStreamCapture("http://localhost:8081", mat => OnEyeFrame(mat, false));
        _leftEyeStream.Start();
        _rightEyeStream.Start();
    }

    private void OnEyeFrame(OpenCvSharp.Mat mat, bool isLeft)
    {
        // Called when a new eye camera frame arrives; synchronize with latest gaze
        if (!_isGazeTraining || _gazeCapture == null) return;
        _gazeCapture.CaptureSample();
        var gaze = _gazeCapture.GetSamples().Count > 0 ? _gazeCapture.GetSamples()[^1] : null;
        if (gaze == null) return;
        var sample = new TrainingSample
        {
            Timestamp = gaze.Timestamp,
            LeftEyeImage = isLeft ? MatToBytes(mat) : Array.Empty<byte>(),
            RightEyeImage = !isLeft ? MatToBytes(mat) : Array.Empty<byte>(),
            HeadPosition = gaze.HeadPosition,
            HeadRotation = gaze.HeadRotation,
            GazeOrigin = gaze.GazeOrigin,
            GazeDirection = gaze.GazeDirection,
            TargetPosition = gaze.TargetPosition
        };
        _samples.Add(sample);
        _dataWriter?.WriteSample(sample);
    }

    private byte[] MatToBytes(OpenCvSharp.Mat mat)
    {
        return mat.ImEncode(".jpg");
    }

    private System.Numerics.Vector3 GetHeadPosition() => Input.Head.position;
    private System.Numerics.Quaternion GetHeadRotation() => Input.Head.orientation; 
    private System.Numerics.Vector3 GetSpherePosition() => new System.Numerics.Vector3(0, 0, 3);
    private Vector3 GetPointInFrontOfUser(float distance = 1f) => Input.Head.position + (Input.Head.Forward * distance);

    private System.Func<float>? _trainerProgressFunc;
    private bool _showTrainingGraph = false;
    private DateTime _trainingStartTime;

    public void ShowTrainingGraph(System.Func<float> getTrainerProgress)
    {
        _trainerProgressFunc = getTrainerProgress;
        _showTrainingGraph = true;
        _trainingStartTime = DateTime.UtcNow;
    }

    public void Update()
    {
        // VR rendering logic here
        if (_isGazeTraining)
        {
            var elapsed = DateTime.UtcNow - _gazeStartTime;
            float progress = (float)(elapsed.TotalSeconds / _gazeDuration.TotalSeconds);
            _progressCallback?.Invoke(progress);
            // Render sphere and progress bar here
            if (progress >= 1.0f)
            {
                StopGazeTraining();
            }
        }
        if (_showTrainingGraph && _trainerProgressFunc != null)
        {
            float trainerProgress = _trainerProgressFunc();
            TimeSpan elapsed = DateTime.UtcNow - _trainingStartTime;
            double etaSeconds = (trainerProgress > 0) ? elapsed.TotalSeconds * (1.0 - trainerProgress) / trainerProgress : 0;
            string etaText = trainerProgress < 1.0f ? $"ETA: {TimeSpan.FromSeconds(etaSeconds):mm:ss}" : "Complete!";

            // Render a simple progress bar and text in VR using StereoKit
            var pose = new Pose(new Vec3(0, 0, 2), Quat.Identity);
            UI.WindowBegin("Training Progress", ref pose, new Vec2(40, 20));
            UI.Text($"Training: {(int)(trainerProgress * 100)}%\n{etaText}");
            UI.SameLine();
            UI.ProgressBar(trainerProgress, 0.33f);
            UI.WindowEnd();
        }
    }

    private void StopGazeTraining()
    {
        _isGazeTraining = false;
        _leftEyeStream?.Stop();
        _rightEyeStream?.Stop();
        _dataWriter?.Dispose();
        _gazeCapture = null;
        _leftEyeStream = null;
        _rightEyeStream = null;
        _dataWriter = null;
        // TODO: Signal handoff to trainer
    }

    public void Shutdown()
    {
        // TODO: Cleanup resources
    }
}
