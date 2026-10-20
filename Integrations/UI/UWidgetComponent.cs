using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>A reusable control. Messages and unhandled input bubble to its owning screen.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class UWidgetComponent : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private bool focusable = true;
        [SerializeField] private int tabOrder;
        [SerializeField] private Selectable selectionTarget;
        [SerializeField] private bool navigationGroup;
        public UCanvasComponent OwnerCanvas { get; private set; }
        public UController OwningController => OwnerCanvas != null ? OwnerCanvas.OwningController : null;
        public virtual bool Focusable => focusable;
        public int TabOrder => tabOrder;
        public bool NavigationGroup => navigationGroup;
        public bool IsFocused { get; private set; }
        public Selectable SelectionTarget => selectionTarget != null ? selectionTarget : GetComponentInChildren<Selectable>();
        public bool CanFocus => isActiveAndEnabled && Focusable && SelectionTarget != null &&
            SelectionTarget.IsActive() && SelectionTarget.IsInteractable();
        public UWidgetComponent ParentWidget
        {
            get
            {
                for (var parent = transform.parent; parent != null; parent = parent.parent)
                {
                    if (parent.GetComponent<UCanvasComponent>() != null) break;
                    var widget = parent.GetComponent<UWidgetComponent>();
                    if (widget != null) return widget;
                }
                return null;
            }
        }

        public void RefreshOwner()
        {
            var owner = GetComponentInParent<UCanvasComponent>();
            if (!isActiveAndEnabled) owner = null;
            if (owner == OwnerCanvas) return;
            if (OwnerCanvas != null) OwnerCanvas.UnregisterWidget(this);
            OwnerCanvas = owner;
            if (OwnerCanvas != null) OwnerCanvas.RegisterWidget(this);
        }

        public bool RequestFocus() => OwnerCanvas != null && OwnerCanvas.FocusWidget(this);
        public void OnSelect(BaseEventData eventData) { if (OwnerCanvas != null) OwnerCanvas.SyncSelection(); }
        public virtual bool HandleInput(PlayerInputEvent input) => false;
        protected virtual bool HandleMessage<T>(UWidgetComponent source, T message) => false;
        public bool Raise<T>(T message)
        {
            for (var parent = ParentWidget; parent != null; parent = parent.ParentWidget)
                if (parent.HandleMessage(this, message)) return true;
            return OwnerCanvas != null && OwnerCanvas.ReceiveMessage(this, message);
        }
        internal void SetFocused(bool value)
        {
            if (IsFocused == value) return;
            IsFocused = value;
            if (value) OnFocusGained(); else OnFocusLost();
        }
        protected virtual void OnFocusGained() { }
        protected virtual void OnFocusLost() { }
        protected virtual void OnEnable() => RefreshOwner();
        protected virtual void OnDisable() => RefreshOwner();
        protected virtual void OnDestroy()
        {
            if (OwnerCanvas != null) OwnerCanvas.UnregisterWidget(this);
            OwnerCanvas = null;
        }
        protected virtual void OnTransformParentChanged()
        {
            // Moving a composite also changes ownership of all its descendants.
            foreach (var widget in GetComponentsInChildren<UWidgetComponent>(true)) widget.RefreshOwner();
        }
    }
}
