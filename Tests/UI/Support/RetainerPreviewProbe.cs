using UnityEngine;

namespace UnscriptedEngine.Tests
{
    [ExecuteAlways]
    public sealed class RetainerPreviewProbe : MonoBehaviour
    {
        public static int EnableCount { get; private set; }
        public static void ResetCount() => EnableCount = 0;
        private void OnEnable() => EnableCount++;
    }
}
