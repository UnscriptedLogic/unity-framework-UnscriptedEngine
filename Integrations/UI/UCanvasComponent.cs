using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>Local-player widget ownership, ordering, focus and cancellable input routing.</summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class UCanvasComponent : MonoBehaviour
    {
        public const int InputPriority = 1000;
        private readonly List<UWidgetComponent> widgets = new();
        private readonly Dictionary<UWidgetComponent, IDisposable> leases = new();
        private readonly Dictionary<UWidgetComponent, GameObject> backdrops = new();
        private readonly Dictionary<Type, Action<bool>> subscriptions = new();
        private readonly Dictionary<WidgetLayer, RectTransform> layers = new();
        private RectTransform staging;
        private UWidgetComponent focused;
        private bool destroying;
        public UController OwningController { get; private set; }
        public UWidgetComponent FocusedWidget => focused;

        public void Initialize(UController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (widgets.Count != 0 && OwningController != controller)
                throw new InvalidOperationException("Detach widgets before changing the owning controller.");
            OwningController = controller;
        }

        private void Awake()
        {
            staging = CreateRect("Inactive staging", transform);
            staging.gameObject.SetActive(false);
            foreach (WidgetLayer layer in Enum.GetValues(typeof(WidgetLayer)))
                layers.Add(layer, CreateRect(layer.ToString(), transform));
            RegisterInput<PlayerMoveInputEvent>();
            RegisterInput<PlayerLookInputEvent>();
            RegisterInput<PlayerSprintInputEvent>();
            RegisterInput<PlayerJumpInputEvent>();
            RegisterInput<PlayerInteractInputEvent>();
        }

        public void RegisterInput<T>() where T : PlayerInputEvent
        {
            if (subscriptions.ContainsKey(typeof(T))) return;
            Action<T> listener = input => RouteInput(input);
            Action<bool> bind = enabled =>
            {
                if (enabled) EventBus.Subscribe(listener, InputPriority);
                else EventBus.Unsubscribe(listener);
            };
            subscriptions.Add(typeof(T), bind);
            if (isActiveAndEnabled) bind(true);
        }

        public T AttachWidget<T>(T prefab) where T : UWidgetComponent => AttachWidget(prefab, null);

        public T AttachWidget<T>(T prefab, Action<T> initialize) where T : UWidgetComponent
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (OwningController == null) throw new InvalidOperationException("Initialize the canvas with a local controller first.");
            var widget = Instantiate(prefab, staging, false);
            widget.gameObject.SetActive(false);
            if (widget.GetComponent<CanvasGroup>() == null) widget.gameObject.AddComponent<CanvasGroup>();
            widget.Canvas = this;
            try
            {
                // Legacy screen prefabs can retain their Canvas but cannot escape stack ordering.
                foreach (var canvas in widget.GetComponentsInChildren<Canvas>(true)) canvas.overrideSorting = false;
                widget.Attach();
                initialize?.Invoke(widget);
                widgets.Add(widget);
                var parent = layers[widget.Layer];
                if (widget.InputPolicy == WidgetInputPolicy.Modal)
                {
                    var backdrop = CreateRect("Modal backdrop", parent);
                    var image = backdrop.gameObject.AddComponent<Image>();
                    image.color = Color.clear;
                    image.raycastTarget = true;
                    backdrops.Add(widget, backdrop.gameObject);
                }
                widget.transform.SetParent(parent, false);
                Stretch((RectTransform)widget.transform);
                widget.gameObject.SetActive(true);
                RefreshWidgets();
                return widget;
            }
            catch
            {
                ForgetWidget(widget);
                DestroyOwnedObject(widget.gameObject);
                throw;
            }
        }

        public void DetachWidget(UWidgetComponent widget)
        {
            if (widget == null || widget.Canvas != this) return;
            widget.gameObject.SetActive(false);
            ForgetWidget(widget);
            DestroyOwnedObject(widget.gameObject);
        }

        internal void ForgetWidget(UWidgetComponent widget)
        {
            if (widget.Canvas != this) return;
            widgets.Remove(widget);
            if (leases.Remove(widget, out var lease)) lease.Dispose();
            if (backdrops.Remove(widget, out var backdrop) && backdrop != null)
            {
                backdrop.SetActive(false);
                DestroyOwnedObject(backdrop);
            }
            widget.SetFocused(false);
            widget.Canvas = null;
            widget.Detach();
            if (!destroying) RefreshWidgets();
        }

        public void BringToFront(UWidgetComponent widget)
        {
            if (widget == null || widget.Canvas != this) return;
            widgets.Remove(widget);
            widgets.Add(widget);
            RefreshWidgets();
        }

        public void RouteInput(PlayerInputEvent input)
        {
            if (!isActiveAndEnabled || input.IsCancelled || input.Controller != OwningController) return;
            var ordered = OrderedWidgets();
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                var widget = ordered[i];
                if (widget == null || widget.Canvas != this || !widget.isActiveAndEnabled ||
                    widget.InputPolicy == WidgetInputPolicy.Passive) continue;
                bool modal = widget.InputPolicy == WidgetInputPolicy.Modal;
                if (widget.HandleInput(input) || input.IsCancelled || modal)
                {
                    input.Cancel();
                    return;
                }
            }
        }

        private List<UWidgetComponent> OrderedWidgets()
        {
            // Stable insertion preserves attachment order within each layer.
            var ordered = new List<UWidgetComponent>();
            foreach (WidgetLayer layer in Enum.GetValues(typeof(WidgetLayer)))
                foreach (var widget in widgets)
                    if (widget != null && widget.Layer == layer) ordered.Add(widget);
            return ordered;
        }

        internal void RefreshWidgets()
        {
            if (destroying) return;
            var ordered = OrderedWidgets();
            UWidgetComponent nextFocus = null;
            bool covered = false;
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                var widget = ordered[i];
                bool shown = isActiveAndEnabled && widget.isActiveAndEnabled;
                bool interactive = shown && !covered && widget.InputPolicy != WidgetInputPolicy.Passive;
                var group = widget.GetComponent<CanvasGroup>();
                group.interactable = interactive;
                group.blocksRaycasts = interactive;
                if (interactive && nextFocus == null) nextFocus = widget;
                bool modal = shown && widget.InputPolicy == WidgetInputPolicy.Modal;
                if (modal && !leases.ContainsKey(widget)) leases.Add(widget, InputFocus.Acquire(widget, OwningController));
                if (!modal && leases.Remove(widget, out var lease)) lease.Dispose();
                if (backdrops.TryGetValue(widget, out var backdrop)) backdrop.SetActive(modal && !covered);
                if (modal) covered = true;
            }
            foreach (var widget in ordered)
            {
                if (backdrops.TryGetValue(widget, out var backdrop)) backdrop.transform.SetAsLastSibling();
                widget.transform.SetAsLastSibling();
            }
            if (focused == nextFocus) return;
            var eventSystem = EventSystem.current;
            if (focused != null)
            {
                var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                if (selected != null && selected.transform.IsChildOf(focused.transform)) focused.LastSelection = selected;
                focused.SetFocused(false);
            }
            focused = nextFocus;
            if (focused != null) focused.SetFocused(true);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(focused != null ? focused.Selection : null);
        }

        private void OnEnable()
        {
            // Awake can register while enabled; unsubscribe first to keep one listener per type.
            foreach (var bind in subscriptions.Values) { bind(false); bind(true); }
            RefreshWidgets();
        }

        private void OnDisable()
        {
            foreach (var bind in subscriptions.Values) bind(false);
            RefreshWidgets();
        }

        private void OnDestroy()
        {
            destroying = true;
            foreach (var bind in subscriptions.Values) bind(false);
            foreach (var lease in leases.Values) lease.Dispose();
            leases.Clear();
            foreach (var widget in widgets.ToArray())
                if (widget != null)
                {
                    widget.gameObject.SetActive(false);
                    widget.Canvas = null;
                    widget.SetFocused(false);
                    widget.Detach();
                }
            widgets.Clear();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static void DestroyOwnedObject(GameObject obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
