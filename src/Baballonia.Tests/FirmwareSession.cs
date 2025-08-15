using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Baballonia.Tests;

/// <summary>
/// Thread safe, async supported Session object for sending and receiving commands in json format
/// </summary>
public class FirmwareSession
{
    private ICommandSender? _commandSender;
    private ILogger _logger;

    private SemaphoreSlim _lock = new(1, 1);

    JsonSerializerOptions _options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public FirmwareSession(ICommandSender? commandSender, ILogger logger)
    {
        _commandSender = commandSender;
        _logger = logger;
    }

    private bool JsonHasPrefix(JsonDocument json, string key)
    {
        if (json.RootElement.ValueKind != JsonValueKind.Object) return false;

        foreach (var prop in json.RootElement.EnumerateObject())
        {
            if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void SendCommand(string command)
    {
        var payload = command;
        _logger.LogDebug("Sending payload: {}", payload);
        _commandSender.WriteLine(payload);
    }

    private JsonDocument ReadResponse(string responseJsonRootKey)
    {
        JsonExtractor jsonExtractor = new JsonExtractor();
        while (true)
        {
            Thread.Sleep(10); // give it some breathing time

            JsonDocument json = jsonExtractor.ReadUntilValidJson(() => _commandSender.ReadLine());
            _logger.LogDebug("Recieved json: {}", json.RootElement.GetRawText());
            if (JsonHasPrefix(json, responseJsonRootKey))
                return json;
        }
    }

    public JsonDocument? WaitForHeartbeat()
    {
        return WaitForHeartbeat(new TimeSpan(5000));
    }

    public JsonDocument? WaitForHeartbeat(TimeSpan timeout)
    {
        _lock.Wait();
        try
        {
            var startTime = DateTime.Now;
            JsonExtractor jsonExtractor = new JsonExtractor();
            while (true)
            {
                if (DateTime.Now - startTime > timeout)
                    throw new TimeoutException("Timeout reached");

                JsonDocument json = jsonExtractor.ReadUntilValidJson(() => _commandSender.ReadLine());
                _logger.LogDebug("Received json: {}", json.RootElement.GetRawText());
                if (JsonHasPrefix(json, "heartbeat"))
                    return json;
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError("Timeout reached");
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public string? SendCommand(IFirmwareRequest request)
    {
        _lock.Wait();
        try
        {
            var genericReqList = new { commands = new[] { request } };
            var serialized = JsonSerializer.Serialize(genericReqList, _options);
            SendCommand(serialized);
            var jsonDoc = ReadResponse("results");
            var response = jsonDoc.Deserialize<FirmwareResponses.GenericResponse>();

            return response.results.First();
        }
        catch (TimeoutException ex)
        {
            _logger.LogError("Timeout reached");
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public T? SendCommand<T>(IFirmwareRequest<T> request)
    {
        _lock.Wait();
        try
        {
            var genericReqList = new { commands = new[] { request } };

            var serialized = JsonSerializer.Serialize(genericReqList, _options);
            SendCommand(serialized);

            // special case because a list of networks comes in a separate json
            if (typeof(T) == typeof(FirmwareResponses.WifiNetworkResponse))
            {
                var networks = ReadResponse("networks");
                ReadResponse("results"); // to discard the actual response
                return networks.Deserialize<T>();
            }

            var jsonDoc = ReadResponse("results");
            var response = jsonDoc.Deserialize<FirmwareResponses.GenericResponse>();
            return JsonSerializer.Deserialize<T>(response.results.First());
        }
        catch (TimeoutException ex)
        {
            _logger.LogError("Timeout reached");
            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T?> SendCommandAsync<T>(IFirmwareRequest<T> request)
    {
        return await Task.Run(() =>
            SendCommand(request)
        );
    }

    public async Task<JsonDocument?> WaitForHeartbeatAsync()
    {
        return await Task.Run(() => WaitForHeartbeat());
    }
    public async Task<JsonDocument?> WaitForHeartbeatAsync(TimeSpan timeout)
    {
        return await Task.Run(() => WaitForHeartbeat(timeout));
    }

    public void Dispose()
    {
        if (_commandSender != null)
            _commandSender.Dispose();
    }
}
