using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CineScape
{
    public static class QualitySettingsController
    {
        public static void Apply(int level)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            asset.renderScale = level == 0 ? .7f : level == 1 ? .85f : 1f;
            asset.msaaSampleCount = level == 2 ? 4 : level == 1 ? 2 : 1;
            asset.shadowDistance = 0;
        }
    }
}
