public enum AppState
{
    Idle,
    IntroVideo,
    GazeTraining,
    Training,
    TrainingGraph,
    Error
}

public class StateCoordinator
{
    public AppState CurrentState { get; private set; } = AppState.Idle;
    private readonly OverlayManager _overlay;
    private readonly TrainerManager _trainer;

    public StateCoordinator(OverlayManager overlay, TrainerManager trainer)
    {
        _overlay = overlay;
        _trainer = trainer;
    }

    public void StartIntro()
    {
        CurrentState = AppState.IntroVideo;
    }

    public void StartGazeTraining()
    {
        CurrentState = AppState.GazeTraining;
        _overlay.StartGazeTraining(OnGazeProgress);
    }

    public void StartTraining(string dataPath, string outputModelPath)
    {
        CurrentState = AppState.Training;
        _trainer.StartTrainingAsync(dataPath, outputModelPath);
        _overlay.ShowTrainingGraph(() => _trainer.Progress);
    }

    private void OnGazeProgress(float progress)
    {
        // TODO: Handle progress bar update, transition to training
    }

    public void SetError(string error)
    {
        CurrentState = AppState.Error;
        // TODO: Display error in overlay
    }
}
