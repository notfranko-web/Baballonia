

using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using StereoKit;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class RestApiServer
{
    private readonly OverlayManager _overlay;
    private readonly TrainerManager _trainer;
    private readonly StateCoordinator _coordinator;
    public static HttpListener listener;
    public static string url = "http://localhost:8000/";

    public RestApiServer(OverlayManager overlay, TrainerManager trainer, StateCoordinator coordinator)
    {
        _overlay = overlay;
        _trainer = trainer;
        _coordinator = coordinator;

        listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
    }

    public async Task StartAsync()
    {
        bool runServer = true;
        while (runServer)
        {
            HttpListenerContext ctx = await listener.GetContextAsync();
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;
            string responseString = "";
            resp.ContentType = "application/json";
            try
            {
                switch (req.Url.AbsolutePath.ToLower())
                {
                    case "/status":
                        if (req.HttpMethod == "GET")
                        {
                            responseString = $"{{\"state\":\"{_coordinator.CurrentState}\",\"isTraining\":{_trainer.IsTraining.ToString().ToLower()},\"progress\":{_trainer.Progress},\"error\":\"{_trainer.LastError ?? ""}\"}}";
                        }
                        break;
                    case "/progress":
                        if (req.HttpMethod == "GET")
                        {
                            responseString = $"{{\"progress\":{_trainer.Progress}}}";
                        }
                        break;
                    case "/start-training":
                        if (req.HttpMethod == "POST")
                        {
                            if (_trainer.IsTraining)
                            {
                                responseString = "{\"started\":false,\"error\":\"Already training\"}";
                            }
                            else
                            {
                                // Example: hardcoded file paths, replace as needed
                                string dataPath = "training_data.bin";
                                string outputModelPath = "model.onnx";
                                _coordinator.StartTraining(dataPath, outputModelPath);
                                responseString = "{\"started\":true}";
                            }
                        }
                        break;
                    case "/shutdown":
                        if (req.HttpMethod == "POST")
                        {
                            responseString = "{\"shutdown\":true}";
                            runServer = false;
                        }
                        break;
                    default:
                        resp.StatusCode = 404;
                        responseString = "{\"error\":\"Not found\"}";
                        break;
                }
                byte[] data = System.Text.Encoding.UTF8.GetBytes(responseString);
                resp.ContentEncoding = System.Text.Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                resp.StatusCode = 500;
                string errorJson = $"{{\"error\":\"{ex.Message}\"}}";
                byte[] data = System.Text.Encoding.UTF8.GetBytes(errorJson);
                resp.ContentEncoding = System.Text.Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
            }
            finally
            {
                resp.OutputStream.Close();
            }
        }
        listener.Stop();
    }

    public async Task StopAsync()
    {
        listener.Close();
    }
}
