using UnityEngine;

namespace UnscriptedEngine.UI
{
    public enum WidgetLayer { HUD, Screen, Modal, Overlay }
    public enum WidgetInputPolicy { Passive, HandleOnly, Modal }

    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class UWidgetComponent : MonoBehaviour
    {
        [SerializeField] private WidgetLayer layer = WidgetLayer.Screen;
        [SerializeField] private WidgetInputPolicy inputPolicy = WidgetInputPolicy.HandleOnly;
        [SerializeField] private GameObject defaultSelection;

        public UCanvasComponent Canvas { get; internal set; }
        public UController OwningController => Canvas != null ? Canvas.OwningController : null;
        public virtual WidgetLayer Layer => layer;
        public virtual WidgetInputPolicy InputPolicy => inputPolicy;
        public bool IsFocused { get; private set; }
        internal GameObject LastSelection;
        internal GameObject Selection
        {
            get
            {
                if (LastSelection != null && LastSelection.activeInHierarchy) return LastSelection;
                if (defaultSelection != null && defaultSelection.activeInHierarchy) return defaultSelection;
                foreach (var selectable in GetComponentsInChildren<UnityEngine.UI.Selectable>())
                    if (selectable.IsActive() && selectable.IsInteractable()) return selectable.gameObject;
                return null;
            }
        }

        public void Close()
        {
            if (Canvas != null) Canvas.DetachWidget(this);
            else Destroy(gameObject);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
        public virtual bool HandleInput(PlayerInputEvent input) => false;
        protected virtual void OnAttached() { }
        protected virtual void OnDetached() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
        protected virtual void OnFocusGained() { }
        protected virtual void OnFocusLost() { }

        internal void Attach() => OnAttached();
        internal void Detach() => OnDetached();
        internal void SetFocused(bool focused)
        {
            if (IsFocused == focused) return;
            IsFocused = focused;
            if (focused) OnFocusGained(); else OnFocusLost();
        }

        protected virtual void OnEnable()
        {
            if (Canvas == null) return;
            Canvas.RefreshWidgets();
            OnShown();
        }

        protected virtual void OnDisable()
        {
            if (Canvas == null) return;
            Canvas.RefreshWidgets();
            OnHidden();
        }

        protected virtual void OnDestroy()
        {
            if (Canvas != null) Canvas.ForgetWidget(this);
        }
    }
}
