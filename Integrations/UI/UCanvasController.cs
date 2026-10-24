using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnscriptedEngine.UI
{
    /// <summary>Local-player screen ownership, ordering, focus and cancellable input routing.</summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public class UCanvasController : MonoBehaviour
    {
        public const int InputPriority = 1000;

        private readonly List<UCanvasComponent> canvases = new();
        private readonly Dictionary<UCanvasComponent, IDisposable> leases = new();
        private readonly Dictionary<UCanvasComponent, GameObject> backdrops = new();
        private readonly Dictionary<Type, Action<bool>> subscriptions = new();
        private readonly Dictionary<CanvasLayer, RectTransform> layers = new();
        private RectTransform staging;
        private UCanvasComponent focused;
        private bool destroying;
        private bool hierarchyDirty;

        public UController OwningController { get; private set; }
        public UCanvasComponent FocusedCanvas => focused;

        private sealed class Binding
        {
            public string ActionKey;
            public bool Toggle;
            public bool AllowDuringModal;
            public Func<UCanvasComponent> Open;
            public UCanvasComponent Instance;
        }

        private readonly List<Binding> bindings = new();

        public string BackAction { get; set; }
        public string NextWidgetAction { get; set; }
        public string PreviousWidgetAction { get; set; }

        public static UCanvasController Create(UController owner)
        {
            GameObject root = new GameObject("Player UI", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");

            UCanvasController controller = root.AddComponent<UCanvasController>();
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;

            controller.Initialize(owner);

            return controller;
        }

        public void BindToggle<T>(string actionKey, T prefab, Action<T> initialize = null,
            bool allowDuringModal = false) where T : UCanvasComponent =>
            Bind(actionKey, () => Show(prefab, initialize), true, allowDuringModal);

        public void BindOpen<T>(string actionKey, T prefab, Action<T> initialize = null,
            bool allowDuringModal = false) where T : UCanvasComponent =>
            Bind(actionKey, () => Show(prefab, initialize), false, allowDuringModal);

        /// <summary>Factory bindings support adapters for third-party UI. Factories must return a screen owned here.</summary>
        public void Bind(string actionKey, Func<UCanvasComponent> open, bool toggle = false, bool allowDuringModal = false)
        {
            if (string.IsNullOrWhiteSpace(actionKey)) throw new ArgumentException("An action key is required.", nameof(actionKey));
            if (open == null) throw new ArgumentNullException(nameof(open));

            if (bindings.Exists(binding => string.Equals(binding.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("An action is already bound: " + actionKey);

            bindings.Add(new Binding
            {
                ActionKey = actionKey,
                Open = open,
                Toggle = toggle,
                AllowDuringModal = allowDuringModal
            });
        }

        private static bool Matches(PlayerActionInputEvent input, string key) =>
            !string.IsNullOrEmpty(key) && string.Equals(input.ActionKey, key, StringComparison.OrdinalIgnoreCase);

        private Binding FindBinding(PlayerActionInputEvent input) => bindings.Find(binding => Matches(input, binding.ActionKey));

        private void ExecuteBinding(Binding binding)
        {
            if (binding.Instance != null && binding.Instance.Controller == this)
            {
                if (!binding.Instance.isActiveAndEnabled) binding.Instance.Show();
                else if (binding.Toggle && binding.Instance == focused) Close(binding.Instance);
                return;
            }

            binding.Instance = binding.Open();
            if (binding.Instance == null || binding.Instance.Controller != this)
                throw new InvalidOperationException("UI binding factory must return a screen owned by this controller.");
        }

        public void Initialize(UController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (canvases.Count != 0 && OwningController != controller)
                throw new InvalidOperationException("Detach canvases before changing the owning controller.");

            OwningController = controller;
        }

        protected virtual void Awake()
        {
            staging = CreateRect("Inactive staging", transform);
            staging.gameObject.SetActive(false);

            foreach (CanvasLayer layer in Enum.GetValues(typeof(CanvasLayer)))
                layers.Add(layer, CreateRect(layer.ToString(), transform));

            RegisterInput<PlayerActionInputEvent>();
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

        public T Show<T>(T prefab) where T : UCanvasComponent => Show(prefab, null);

        public T Show<T>(T prefab, Action<T> initialize) where T : UCanvasComponent
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (OwningController == null) throw new InvalidOperationException("Initialize the canvas with a local controller first.");

            T screen = Instantiate(prefab, staging, false);
            return AttachInstance(screen, initialize);
        }

        public T CreateCanvas<T>(Action<T> initialize = null) where T : UCanvasComponent
        {
            if (OwningController == null) throw new InvalidOperationException("Initialize the controller first.");

            GameObject root = new GameObject(typeof(T).Name, typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(staging, false);
            return AttachInstance(root.AddComponent<T>(), initialize);
        }

        private T AttachInstance<T>(T screen, Action<T> initialize) where T : UCanvasComponent
        {
            screen.gameObject.SetActive(false);
            if (screen.GetComponent<CanvasGroup>() == null) screen.gameObject.AddComponent<CanvasGroup>();
            screen.Controller = this;

            try
            {
                screen.Attach();
                initialize?.Invoke(screen);

                // Includes third-party UI instantiated by the initialization callback.
                foreach (Canvas canvas in screen.GetComponentsInChildren<Canvas>(true)) canvas.overrideSorting = false;

                canvases.Add(screen);
                RectTransform parent = layers[screen.Layer];

                if (screen.InputPolicy == CanvasInputPolicy.Modal)
                {
                    RectTransform backdrop = CreateRect("Modal backdrop", parent);
                    Image image = backdrop.gameObject.AddComponent<Image>();
                    image.color = Color.clear;
                    image.raycastTarget = true;
                    backdrops.Add(screen, backdrop.gameObject);
                }

                screen.transform.SetParent(parent, false);
                Stretch((RectTransform)screen.transform);
                screen.gameObject.SetActive(true);
                RefreshCanvases();

                return screen;
            }
            catch
            {
                ForgetCanvas(screen);
                DestroyOwnedObject(screen.gameObject);
                throw;
            }
        }

        public void Close(UCanvasComponent screen)
        {
            if (screen == null || screen.Controller != this) return;

            screen.gameObject.SetActive(false);
            ForgetCanvas(screen);
            DestroyOwnedObject(screen.gameObject);
        }

        internal void ForgetCanvas(UCanvasComponent screen)
        {
            if (screen.Controller != this) return;

            canvases.Remove(screen);
            if (leases.Remove(screen, out IDisposable lease)) lease.Dispose();
            if (backdrops.Remove(screen, out GameObject backdrop) && backdrop != null)
            {
                backdrop.SetActive(false);
                DestroyOwnedObject(backdrop);
            }

            screen.SetFocused(false);
            screen.Controller = null;
            screen.Detach();

            if (!destroying) RefreshCanvases();
        }

        public void BringToFront(UCanvasComponent screen)
        {
            if (screen == null || screen.Controller != this) return;
            canvases.Remove(screen);
            canvases.Add(screen);
            RefreshCanvases();
        }

        public void RouteInput(PlayerInputEvent input)
        {
            if (!isActiveAndEnabled || input.IsCancelled || input.Controller != OwningController) return;

            PlayerActionInputEvent action = input as PlayerActionInputEvent;
            bool pressed = action != null && action.IsPerformed && action.IsPressed && !action.IsSynthetic;
            Binding binding = pressed ? FindBinding(action) : null;

            // Explicit global bindings (e.g. the developer console) can cross modal boundaries.
            if (binding != null && binding.AllowDuringModal)
            {
                input.Cancel();
                ExecuteBinding(binding);
                return;
            }

            List<UCanvasComponent> ordered = OrderedCanvases();
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                UCanvasComponent screen = ordered[i];
                if (screen == null || screen.Controller != this || !screen.isActiveAndEnabled ||
                    screen.InputPolicy == CanvasInputPolicy.Passive) continue;

                bool modal = screen.InputPolicy == CanvasInputPolicy.Modal;
                if (screen.RouteInput(input) || input.IsCancelled)
                {
                    input.Cancel();
                    return;
                }
                if (screen == null || screen.Controller != this || !screen.isActiveAndEnabled)
                {
                    // A screen may close itself from an input callback without returning handled.
                    input.Cancel();
                    return;
                }
                if (pressed && screen == focused)
                {
                    if (Matches(action, NextWidgetAction) || Matches(action, PreviousWidgetAction))
                    {
                        screen.Navigate(Matches(action, PreviousWidgetAction));
                        input.Cancel();
                        return;
                    }
                    if (Matches(action, BackAction))
                    {
                        // Consume before dismissal so this press cannot reopen the same screen.
                        if (screen.Dismissible)
                        {
                            input.Cancel();
                            screen.HandleBack();
                            return;
                        }
                    }
                    if (binding != null && binding.Instance == screen)
                    {
                        input.Cancel();
                        ExecuteBinding(binding);
                        return;
                    }
                }
                if (modal)
                {
                    input.Cancel();
                    return;
                }
            }

            if (binding != null)
            {
                input.Cancel();
                ExecuteBinding(binding);
            }
        }

        private List<UCanvasComponent> OrderedCanvases()
        {
            // Stable insertion preserves attachment order within each layer.
            List<UCanvasComponent> ordered = new List<UCanvasComponent>();
            foreach (CanvasLayer layer in Enum.GetValues(typeof(CanvasLayer)))
                foreach (UCanvasComponent screen in canvases)
                    if (screen != null && screen.Layer == layer) ordered.Add(screen);

            return ordered;
        }

        internal void RefreshCanvases()
        {
            if (destroying) return;

            // OnEnable/OnDisable can run while Unity is traversing a parent hierarchy.
            // Update input state immediately, but defer hierarchy mutations until LateUpdate.
            hierarchyDirty = true;

            List<UCanvasComponent> ordered = OrderedCanvases();
            UCanvasComponent nextFocus = null;
            bool covered = false;

            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                UCanvasComponent screen = ordered[i];
                bool shown = isActiveAndEnabled && screen.isActiveAndEnabled;
                bool interactive = shown && !covered && screen.InputPolicy != CanvasInputPolicy.Passive;

                CanvasGroup group = screen.GetComponent<CanvasGroup>();
                group.interactable = interactive;
                group.blocksRaycasts = interactive;

                if (interactive && nextFocus == null) nextFocus = screen;

                bool modal = shown && screen.InputPolicy == CanvasInputPolicy.Modal;
                if (modal && !leases.ContainsKey(screen)) leases.Add(screen, InputFocus.Acquire(screen, OwningController));
                if (!modal && leases.Remove(screen, out IDisposable lease)) lease.Dispose();
                if (modal) covered = true;
            }

            if (focused == nextFocus) return;

            EventSystem eventSystem = EventSystem.current;
            if (focused != null)
            {
                GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                if (selected != null && selected.transform.IsChildOf(focused.transform)) focused.LastSelection = selected;
                focused.SetFocused(false);
            }

            focused = nextFocus;
            if (focused != null) focused.SetFocused(true);
            if (eventSystem != null) eventSystem.SetSelectedGameObject(focused != null ? focused.Selection : null);
            if (focused != null) focused.SyncSelection();
        }

        protected virtual void LateUpdate()
        {
            if (destroying || !hierarchyDirty) return;

            hierarchyDirty = false;
            List<UCanvasComponent> ordered = OrderedCanvases();
            bool covered = false;

            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                UCanvasComponent screen = ordered[i];
                bool modal = screen.isActiveAndEnabled && screen.InputPolicy == CanvasInputPolicy.Modal;

                if (backdrops.TryGetValue(screen, out GameObject backdrop) && backdrop != null)
                {
                    bool visible = modal && !covered;
                    if (backdrop.activeSelf != visible) backdrop.SetActive(visible);
                }

                if (modal) covered = true;
            }

            foreach (UCanvasComponent screen in ordered)
            {
                if (backdrops.TryGetValue(screen, out GameObject backdrop) && backdrop != null)
                    backdrop.transform.SetAsLastSibling();

                screen.transform.SetAsLastSibling();
            }
        }

        protected virtual void OnEnable()
        {
            // Awake can register while enabled; unsubscribe first to keep one listener per type.
            foreach (Action<bool> bind in subscriptions.Values)
            {
                bind(false);
                bind(true);
            }

            RefreshCanvases();
        }

        protected virtual void OnDisable()
        {
            foreach (Action<bool> bind in subscriptions.Values) bind(false);

            RefreshCanvases();
        }

        protected virtual void OnDestroy()
        {
            destroying = true;

            foreach (Action<bool> bind in subscriptions.Values) bind(false);
            foreach (IDisposable lease in leases.Values) lease.Dispose();
            leases.Clear();

            foreach (UCanvasComponent screen in canvases.ToArray())
                if (screen != null)
                {
                    screen.gameObject.SetActive(false);
                    screen.Controller = null;
                    screen.SetFocused(false);
                    screen.Detach();
                }

            canvases.Clear();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
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

