using System.Collections.Generic;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Enums;

namespace Baballonia.Contracts
{
    public interface ISingleCameraEyeInferenceService : IInferenceService
    {
        /// <summary>
        /// Gets the type of eye inference service (single or dual camera)
        /// </summary>
        EyeInferenceType Type { get; }

        /// <summary>
        /// Gets the camera URL(s) being used by this service
        /// </summary>
        IReadOnlyDictionary<Camera, string> CameraUrls { get; }
    }

    public interface IDualCameraEyeInferenceService : IInferenceService
    {
        /// <summary>
        /// Gets the type of eye inference service (single or dual camera)
        /// </summary>
        EyeInferenceType Type { get; }

        /// <summary>
        /// Gets the camera URL(s) being used by this service
        /// </summary>
        IReadOnlyDictionary<Camera, string> CameraUrls { get; }
    }
}

/// <summary>
/// Type of eye inference service
/// </summary>
public enum EyeInferenceType
{
    /// <summary>
    /// Single camera used for both eyes
    /// </summary>
    SingleCamera,

    /// <summary>
    /// Separate cameras for each eye
    /// </summary>
    DualCamera
}
