using System.Collections.Generic;
using System.Threading.Tasks;

namespace Baballonia.Contracts;

public interface IDeviceEnumerator
{
    public Dictionary<string, string> Cameras { get; set; }
    public Task<Dictionary<string, string>> UpdateCameras();
}
