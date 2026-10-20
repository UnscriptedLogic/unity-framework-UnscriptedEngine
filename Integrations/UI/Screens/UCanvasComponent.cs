using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnscriptedEngine.UI
{
    public enum CanvasLayer { HUD, Screen, Modal, Overlay }
    public enum CanvasInputPolicy { Passive, HandleOnly, Modal }

    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class UCanvasComponent : MonoBehaviour
    {
        [SerializeField] private CanvasLayer layer = CanvasLayer.Screen;
        [SerializeField] private CanvasInputPolicy inputPolicy = CanvasInputPolicy.HandleOnly;
        [SerializeField] private GameObject defaultSelection;

        public UCanvasController Controller { get; internal set; }
        public UController OwningController => Controller != null ? Controller.OwningController : null;
        public virtual CanvasLayer Layer => layer;
        public virtual CanvasInputPolicy InputPolicy => inputPolicy;
        public bool IsFocused { get; private set; }
        public virtual bool Dismissible => true;
        private readonly List<UWidgetComponent> widgets = new();
        public UWidgetComponent FocusedWidget { get; private set; }
        public IReadOnlyList<UWidgetComponent> Widgets => widgets.AsReadOnly();
        internal GameObject LastSelection;
        internal GameObject Selection
        {
            get
            {
                if (ValidSelection(LastSelection)) return LastSelection;
                if (ValidSelection(defaultSelection)) return defaultSelection;
                foreach (var selectable in GetComponentsInChildren<UnityEngine.UI.Selectable>())
                    if (selectable.IsActive() && selectable.IsInteractable()) return selectable.gameObject;
                return null;
            }
        }
        private bool ValidSelection(GameObject target)
        {
            if (target == null || !target.activeInHierarchy || !target.transform.IsChildOf(transform)) return false;
            var selectable = target.GetComponent<UnityEngine.UI.Selectable>();
            return selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }

        public void Close()
        {
            if (Controller != null) Controller.Close(this);
            else Destroy(gameObject);
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
        public virtual bool HandleInput(PlayerInputEvent input) => false;
        public virtual bool HandleBack() { if (!Dismissible) return false; Close(); return true; }
        protected virtual bool HandleMessage<T>(UWidgetComponent source, T message) => false;
        internal bool ReceiveMessage<T>(UWidgetComponent source, T message) => HandleMessage(source, message);

        public void RegisterWidget(UWidgetComponent widget)
        {
            if (widget == null || widget.OwnerCanvas != this || widgets.Contains(widget)) return;
            widgets.Add(widget);
        }

        public void UnregisterWidget(UWidgetComponent widget)
        {
            widgets.Remove(widget);
            if (FocusedWidget != widget) return;
            widget.SetFocused(false);
            FocusedWidget = null;
            if (IsFocused && isActiveAndEnabled) Navigate(false);
        }

        public bool FocusWidget(UWidgetComponent widget)
        {
            if (!IsFocused || widget == null || widget.OwnerCanvas != this || !widget.CanFocus) return false;
            var system = EventSystem.current;
            if (system == null) return false;
            system.SetSelectedGameObject(widget.SelectionTarget.gameObject);
            SyncSelection();
            return true;
        }

        internal void SyncSelection()
        {
            UWidgetComponent next = null;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (IsFocused && selected != null && selected.transform.IsChildOf(transform))
            {
                next = selected.GetComponentInParent<UWidgetComponent>();
                if (next != null && (next.OwnerCanvas != this || !next.isActiveAndEnabled)) next = null;
                LastSelection = selected;
            }
            if (FocusedWidget == next) return;
            if (FocusedWidget != null) FocusedWidget.SetFocused(false);
            FocusedWidget = next;
            if (next != null) next.SetFocused(true);
        }

        public bool Navigate(bool backwards)
        {
            if (!IsFocused) return false;
            SyncSelection();
            var stops = new List<UWidgetComponent>();
            foreach (var widget in widgets)
            {
                if (widget == null || !widget.CanFocus) continue;
                bool grouped = false;
                for (var parent = widget.ParentWidget; parent != null; parent = parent.ParentWidget)
                    if (parent.NavigationGroup && parent.CanFocus) { grouped = true; break; }
                if (!grouped) stops.Add(widget);
            }
            // Preserve registration order for equal TabOrder values.
            for (int i = 1; i < stops.Count; i++)
            {
                var value = stops[i];
                int j = i - 1;
                while (j >= 0 && stops[j].TabOrder > value.TabOrder) { stops[j + 1] = stops[j]; j--; }
                stops[j + 1] = value;
            }
            if (stops.Count == 0) return false;
            var current = FocusedWidget;
            while (current != null && !stops.Contains(current)) current = current.ParentWidget;
            int index = stops.IndexOf(current);
            int nextIndex = index < 0 ? (backwards ? stops.Count - 1 : 0)
                : (index + (backwards ? -1 : 1) + stops.Count) % stops.Count;
            return FocusWidget(stops[nextIndex]);
        }

        internal bool RouteInput(PlayerInputEvent input)
        {
            SyncSelection();
            var chain = new List<UWidgetComponent>();
            for (var widget = FocusedWidget; widget != null; widget = widget.ParentWidget)
                chain.Add(widget);
            foreach (var widget in chain)
                if (widget != null && widget.OwnerCanvas == this && widget.isActiveAndEnabled &&
                    (widget.HandleInput(input) || input.IsCancelled)) return true;
            return this != null && isActiveAndEnabled && HandleInput(input);
        }

        protected virtual void LateUpdate() { if (IsFocused) SyncSelection(); }
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
            if (!focused && FocusedWidget != null) { FocusedWidget.SetFocused(false); FocusedWidget = null; }
            if (focused) OnFocusGained(); else OnFocusLost();
        }

        protected virtual void OnEnable()
        {
            if (Controller == null) return;
            Controller.RefreshCanvases();
            OnShown();
            foreach (var widget in GetComponentsInChildren<UWidgetComponent>(true)) widget.RefreshOwner();
        }

        protected virtual void OnDisable()
        {
            if (Controller == null) return;
            Controller.RefreshCanvases();
            OnHidden();
        }

        protected virtual void OnDestroy()
        {
            if (Controller != null) Controller.ForgetCanvas(this);
        }
    }
}

