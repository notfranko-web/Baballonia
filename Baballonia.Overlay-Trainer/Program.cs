class Program
{
    static async Task Main(string[] args)
    {
        var overlay = new OverlayManager();
        var trainer = new TrainerManager();
        var coordinator = new StateCoordinator(overlay, trainer);
        var restApi = new RestApiServer(overlay, trainer, coordinator);

        overlay.Initialize();
        coordinator.StartIntro();

        // Start REST API server in background
        await restApi.StartAsync();

        // Main VR/render loop
        while (true)
        {
            overlay.Update();
            await Task.Delay(16); // ~60 FPS
        }

        // On shutdown (not reached in this loop)
        // await restApi.StopAsync();
        // overlay.Shutdown();
    }
}
