using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Local input session. Target providers choose focus; interactables own authoritative progress.</summary>
    [DisallowMultipleComponent]
    public class InteractorComponent : MonoBehaviour
    {
        [Tooltip("Component implementing IInteractionTargetProvider. Automatically finds one on this object.")]
        [SerializeField] private MonoBehaviour targetProvider;
        [SerializeField, Min(0.05f)] private float heartbeatInterval = 0.25f;
        private IInteractionTargetProvider provider;
        private bool buttonHeld;
        private float nextHeartbeat;
        private uint sessionId;

        public UInteractableComponent ActiveInteractable { get; private set; }
        public UInteractableComponent FocusedInteractable => targetProvider != null &&
            targetProvider.isActiveAndEnabled ? provider?.FocusedInteractable : null;

        private void Awake()
        {
            ResolveTargetProvider();
        }

        public void SetTargetProvider(MonoBehaviour component)
        {
            InterruptInteraction();
            targetProvider = component;
            provider = component as IInteractionTargetProvider;
        }

        public void SetInteractionHeld(bool held)
        {
            if (!held)
            {
                buttonHeld = false;
                InterruptInteraction();
                return;
            }
            if (buttonHeld || !isActiveAndEnabled) return;
            buttonHeld = true;
            BeginFocusedInteraction();
        }

        // Preserve buttonHeld after interruption: switching focus requires a fresh press.
        public void InterruptInteraction()
        {
            UInteractableComponent target = ActiveInteractable;
            ClearActiveInteraction();
            if (target != null) target.EndInteraction(gameObject, sessionId);
        }

        private void LateUpdate()
        {
            UInteractableComponent target = ActiveInteractable;
            if (target == null) return;
            if (!target.isActiveAndEnabled || target != FocusedInteractable)
            {
                InterruptInteraction();
                return;
            }
            if (Time.unscaledTime < nextHeartbeat) return;
            nextHeartbeat = Time.unscaledTime + Mathf.Max(0.05f, heartbeatInterval);
            target.KeepInteractionAlive(gameObject, sessionId);
        }

        private void BeginFocusedInteraction()
        {
            UInteractableComponent target = FocusedInteractable;
            if (target == null) return;
            sessionId++;
            ActiveInteractable = target;
            target.InteractionCompleted += HandleInteractionEnded;
            target.InteractionInterrupted += HandleInteractionEnded;
            target.InteractionRejected += HandleInteractionRejected;
            nextHeartbeat = Time.unscaledTime;
            if (!target.TryBeginInteraction(gameObject, sessionId)) ClearActiveInteraction();
        }

        private void HandleInteractionEnded(UInteractableComponent.UInteractArgs args)
        {
            if (args.InteractorSource == gameObject && args.SessionId == sessionId) ClearActiveInteraction();
        }

        private void HandleInteractionRejected(GameObject source, uint rejectedSessionId)
        {
            if (source == gameObject && rejectedSessionId == sessionId) ClearActiveInteraction();
        }

        private void ClearActiveInteraction()
        {
            if (ActiveInteractable != null)
            {
                ActiveInteractable.InteractionCompleted -= HandleInteractionEnded;
                ActiveInteractable.InteractionInterrupted -= HandleInteractionEnded;
                ActiveInteractable.InteractionRejected -= HandleInteractionRejected;
            }
            ActiveInteractable = null;
        }

        private void ResolveTargetProvider()
        {
            if (targetProvider != null)
            {
                provider = targetProvider as IInteractionTargetProvider;
                return;
            }
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (!(component is IInteractionTargetProvider candidate)) continue;
                targetProvider = component;
                provider = candidate;
                return;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetInteractionHeld(false);
        }

        private void OnDisable()
        {
            SetInteractionHeld(false);
        }
    }
}
