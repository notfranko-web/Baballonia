using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

public class RestApiServer
{
    private IHost? _host;
    private readonly OverlayManager _overlay;
    private readonly TrainerManager _trainer;
    private readonly StateCoordinator _coordinator;

    public RestApiServer(OverlayManager overlay, TrainerManager trainer, StateCoordinator coordinator)
    {
        _overlay = overlay;
        _trainer = trainer;
        _coordinator = coordinator;
    }

    public async Task StartAsync(int port = 5000)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.Configure(app =>
                {
                    /*app.Map("/status", async context =>
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync($"{{\"state\":\"{_coordinator.CurrentState}\",\"isTraining\":{_trainer.IsTraining.ToString().ToLower()},\"progress\":{_trainer.Progress},\"error\":\"{_trainer.LastError ?? ""}\"}}");
                    });
                    app.Map("/start-training", async context =>
                    {
                        context.Response.ContentType = "application/json";
                        if (_trainer.IsTraining)
                        {
                            await context.Response.WriteAsync("{\"started\":false,\"error\":\"Already training\"}");
                            return;
                        }
                        // Example: hardcoded file paths, replace as needed
                        string dataPath = "training_data.bin";
                        string outputModelPath = "model.onnx";
                        _coordinator.StartTraining(dataPath, outputModelPath);
                        await context.Response.WriteAsync("{\"started\":true}");
                    });
                    app.Map("/progress", async context =>
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync($"{{\"progress\":{_trainer.Progress}}}");
                    });*/
                });
                webBuilder.UseUrls($"http://0.0.0.0:{port}");
            })
            .Build();

        await _host.RunAsync();
    }

    public async Task StopAsync()
    {
        if (_host != null)
            await _host.StopAsync();
    }
}
