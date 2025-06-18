using System.Collections.Generic;

namespace Baballonia.Contracts;

public interface IDeviceEnumerator
{
    public Dictionary<string, string> GetCameras();
}
