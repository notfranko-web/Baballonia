using System;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using Microsoft.Extensions.Logging;

namespace AvaloniaMiaDev.Services;

public class MainStandalone : IMainService
{
    private readonly ILogger<MainStandalone> _logger;

    public Action<string, float> ParameterUpdate { get; set; } = (_, _) => { };

    public MainStandalone(
        ILogger<MainStandalone> logger
        )
    {
        _logger = logger;
    }

    public async Task Teardown()
    {
        _logger.LogInformation("VRCFT Standalone Exiting!");

        _logger.LogDebug("Resetting our time end period...");
        if (OperatingSystem.IsWindows())
        {
            var timeEndRes = Utils.TimeEndPeriod(1);
            if (timeEndRes != 0)
            {
                _logger.LogWarning($"TimeEndPeriod failed with HRESULT {timeEndRes}");
            }
        }

        _logger.LogDebug("Teardown complete. Awaiting exit...");
    }

    public async Task InitializeAsync()
    {
        // Begin main OSC update loop
        _logger.LogDebug("Starting OSC update loop...");
    }
}
