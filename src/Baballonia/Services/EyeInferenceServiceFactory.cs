using System;
using System.Collections.Generic;
using Baballonia.Contracts;
using Baballonia.Services.Inference.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Baballonia.Services
{
    public static class EyeInferenceServiceFactory
    {
        public static IInferenceService Create(IServiceProvider serviceProvider, Dictionary<Camera, string> cameraUrls)
        {
            var leftCameraUrl = cameraUrls.GetValueOrDefault(Camera.Left);
            var rightCameraUrl = cameraUrls.GetValueOrDefault(Camera.Right);

            // If either camera URL is not set or if they're the same, use single camera mode
            if (!string.IsNullOrEmpty(leftCameraUrl) && !string.IsNullOrEmpty(rightCameraUrl))
            {
                if (leftCameraUrl == rightCameraUrl)
                {
                    return serviceProvider.GetRequiredService<ISingleCameraEyeInferenceService>();
                }
            }
            
            return serviceProvider.GetRequiredService<IDualCameraEyeInferenceService>();
        }
    }
}
