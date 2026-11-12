using UnityEngine;

namespace UnscriptedEngine.Audio
{
    [AddComponentMenu("")]
    public sealed class AudioRuntimeDriver : MonoBehaviour
    {
        private void Update() => AudioRuntime.Tick();
        private void OnDestroy() => AudioRuntime.Shutdown();
    }
}
