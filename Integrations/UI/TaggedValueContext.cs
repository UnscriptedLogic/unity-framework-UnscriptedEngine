using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Shared actor source for tagged-value bindings below this UI root.</summary>
    public sealed class TaggedValueContext : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Actor root searched by child bindings. Call SetSource after spawning the UI.")]
        private GameObject sourceRoot;

        public GameObject SourceRoot => sourceRoot;
        public void SetSource(GameObject source) => sourceRoot = source;
    }
}
