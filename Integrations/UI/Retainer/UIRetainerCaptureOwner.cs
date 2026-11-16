using System;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Retains logical ancestry and finishes cleanup outside hierarchy activation callbacks.</summary>
    [AddComponentMenu("")]
    public sealed class UIRetainerCaptureOwner : MonoBehaviour
    {
        private UIRetainer owner;
        internal UIRetainer Owner => owner;
        private Action pendingCleanup;
        internal bool IsDestroying { get; private set; }

        internal void Initialize(UIRetainer value) => owner = value;
        internal void DeferCleanup(Action cleanup) => pendingCleanup = cleanup;
        internal void CancelCleanup() => pendingCleanup = null;
        private void LateUpdate() => FinishCleanup();
        private void OnDestroy() { IsDestroying = true; FinishCleanup(); }
        private void FinishCleanup()
        {
            Action cleanup = pendingCleanup;
            pendingCleanup = null;
            cleanup?.Invoke();
        }

        internal static T FindLogicalParent<T>(Component child) where T : Component
        {
            Transform current = child.transform;
            while (current != null)
            {
                if (current.TryGetComponent(out T result)) return result;
                if (current.TryGetComponent(out UIRetainerCaptureOwner capture) && capture.owner != null)
                    current = capture.owner.transform;
                else current = current.parent;
            }
            return null;
        }
    }
}
