using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnscriptedEngine.UI
{
    /// <summary>Routes a single pointer into captured UI using the shader's output-to-source mapping.</summary>
    [DisallowMultipleComponent]
    public sealed class UIRetainerInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerMoveHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, ICanvasRaycastFilter
    {
        private UIRetainer retainer;
        private PointerEventData mapped;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly List<GameObject> hovered = new List<GameObject>();
        private GameObject pressed;
        private GameObject clicked;
        private GameObject dragged;
        private int? activePointer;

        private void Awake() => retainer = GetComponent<UIRetainer>();

        private bool UpdatePointer(PointerEventData source, bool requireInside = true)
        {
            if (retainer == null || !retainer.isActiveAndEnabled || retainer.Texture == null ||
                (activePointer.HasValue && activePointer.Value != source.pointerId)) return false;
            if (mapped == null) mapped = new PointerEventData(EventSystem.current);
            RectTransform rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, source.position,
                    retainer.DisplayCamera, out Vector2 local)) return false;
            Rect bounds = rect.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return false;
            Vector2 uv = new Vector2((local.x - bounds.xMin) / bounds.width, (local.y - bounds.yMin) / bounds.height);
            Vector2 sourceUV = retainer.MapToSource(uv);
            bool inside = UIRetainerWarp.Contains(uv) && UIRetainerWarp.Contains(sourceUV);
            Vector2 position = Vector2.Scale(sourceUV, new Vector2(retainer.Texture.width, retainer.Texture.height));
            mapped.position = position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, source.position - source.delta,
                retainer.DisplayCamera, out Vector2 previousLocal);
            Vector2 previousUV = new Vector2((previousLocal.x - bounds.xMin) / bounds.width, (previousLocal.y - bounds.yMin) / bounds.height);
            mapped.delta = position - Vector2.Scale(retainer.MapToSource(previousUV), new Vector2(retainer.Texture.width, retainer.Texture.height));
            mapped.pointerId = source.pointerId;
            mapped.button = source.button;
            mapped.scrollDelta = source.scrollDelta;
            mapped.clickCount = source.clickCount;
            mapped.clickTime = source.clickTime;
            mapped.useDragThreshold = source.useDragThreshold;
            mapped.eligibleForClick = source.eligibleForClick;
            mapped.pointerPress = pressed;
            mapped.pointerDrag = dragged;
            hits.Clear();
            if (inside) retainer.CaptureRaycaster.Raycast(mapped, hits);
            mapped.pointerCurrentRaycast = hits.Count > 0 ? hits[0] : new RaycastResult();
            UpdateHover(mapped.pointerCurrentRaycast.gameObject);
            return !requireInside || inside;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (retainer == null || !retainer.isActiveAndEnabled || retainer.Texture == null) return true;
            RectTransform rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out Vector2 local)) return false;
            Rect bounds = rect.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return false;
            Vector2 uv = new Vector2((local.x - bounds.xMin) / bounds.width, (local.y - bounds.yMin) / bounds.height);
            return UIRetainerWarp.Contains(retainer.MapToSource(uv));
        }

        private void UpdateHover(GameObject target)
        {
            // Match uGUI's ancestor enter/exit behavior without a second input module.
            for (int i = hovered.Count - 1; i >= 0; i--)
            {
                GameObject previous = hovered[i];
                if (previous != null && target != null && target.transform.IsChildOf(previous.transform)) continue;
                if (previous != null) ExecuteEvents.Execute(previous, mapped, ExecuteEvents.pointerExitHandler);
                hovered.RemoveAt(i);
            }
            Transform current = target != null ? target.transform : null;
            while (current != null && current != retainer.CaptureRaycaster.transform)
            {
                if (!hovered.Contains(current.gameObject))
                {
                    hovered.Add(current.gameObject);
                    ExecuteEvents.Execute(current.gameObject, mapped, ExecuteEvents.pointerEnterHandler);
                }
                current = current.parent;
            }
            mapped.pointerEnter = target;
        }

        public void OnPointerEnter(PointerEventData eventData) => UpdatePointer(eventData);
        public void OnPointerMove(PointerEventData eventData) => UpdatePointer(eventData);
        public void OnPointerExit(PointerEventData eventData)
        {
            if (mapped != null && (!activePointer.HasValue || activePointer == eventData.pointerId)) UpdateHover(null);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!UpdatePointer(eventData)) return;
            activePointer = eventData.pointerId;
            GameObject target = mapped.pointerCurrentRaycast.gameObject;
            mapped.pressPosition = mapped.position;
            mapped.pointerPressRaycast = mapped.pointerCurrentRaycast;
            clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            pressed = ExecuteEvents.ExecuteHierarchy(target, mapped, ExecuteEvents.pointerDownHandler);
            if (pressed == null) pressed = clicked;
            dragged = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            mapped.pointerPress = pressed;
            mapped.rawPointerPress = target;
            mapped.pointerDrag = dragged;
            GameObject selectable = ExecuteEvents.GetEventHandler<ISelectHandler>(target);
            EventSystem.current.SetSelectedGameObject(selectable, mapped);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (!UpdatePointer(eventData, false)) return;
            ExecuteEvents.Execute(dragged, mapped, ExecuteEvents.initializePotentialDrag);
            eventData.useDragThreshold = mapped.useDragThreshold;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!UpdatePointer(eventData, false)) return;
            mapped.dragging = true;
            ExecuteEvents.Execute(dragged, mapped, ExecuteEvents.beginDragHandler);
            if (pressed != dragged)
            {
                ExecuteEvents.Execute(pressed, mapped, ExecuteEvents.pointerUpHandler);
                pressed = null;
                clicked = null;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (UpdatePointer(eventData, false)) ExecuteEvents.Execute(dragged, mapped, ExecuteEvents.dragHandler);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!UpdatePointer(eventData, false)) return;
            ExecuteEvents.Execute(pressed, mapped, ExecuteEvents.pointerUpHandler);
            if (mapped.dragging)
                ExecuteEvents.ExecuteHierarchy(mapped.pointerCurrentRaycast.gameObject, mapped, ExecuteEvents.dropHandler);
            pressed = null;
            activePointer = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!UpdatePointer(eventData)) { clicked = null; return; }
            if (clicked != null && clicked == ExecuteEvents.GetEventHandler<IPointerClickHandler>(mapped.pointerCurrentRaycast.gameObject))
                ExecuteEvents.Execute(clicked, mapped, ExecuteEvents.pointerClickHandler);
            clicked = null;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!UpdatePointer(eventData, false)) return;
            ExecuteEvents.Execute(dragged, mapped, ExecuteEvents.endDragHandler);
            dragged = null;
            clicked = null;
            mapped.dragging = false;
            activePointer = null;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (UpdatePointer(eventData))
                ExecuteEvents.ExecuteHierarchy(mapped.pointerCurrentRaycast.gameObject, mapped, ExecuteEvents.scrollHandler);
        }

        public void CancelInput()
        {
            if (mapped == null) return;
            ExecuteEvents.Execute(pressed, mapped, ExecuteEvents.pointerUpHandler);
            if (mapped.dragging) ExecuteEvents.Execute(dragged, mapped, ExecuteEvents.endDragHandler);
            UpdateHover(null);
            pressed = clicked = dragged = null;
            activePointer = null;
            mapped = null;
        }

        private void OnDisable() => CancelInput();
    }
}
