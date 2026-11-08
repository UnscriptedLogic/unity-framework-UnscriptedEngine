using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UnscriptedEngine.UI.Editor
{
    internal static class ChestInventoryUIBuilder
    {
        internal static void Build(GameObject chest)
        {
            GameObject canvasObject = Rect("Chest Inventory", chest.transform).gameObject;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            InventoryTransferPanel panel = canvasObject.AddComponent<InventoryTransferPanel>();
            RectTransform backdrop = Rect("Backdrop", canvasObject.transform);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            RectTransform frame = Rect("Inventory Window", backdrop);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1040f, 580f);
            frame.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 1f);

            GameObject slotTemplate = CreateSlotTemplate(canvasObject.transform);
            InventoryView player = CreateView("Player", frame, slotTemplate, new Vector2(0.02f, 0.12f), new Vector2(0.49f, 0.98f));
            InventoryView container = CreateView("Chest", frame, slotTemplate, new Vector2(0.51f, 0.12f), new Vector2(0.98f, 0.98f));
            ChestCreation.SetObject(panel, "playerView", player);
            ChestCreation.SetObject(panel, "containerView", container);
            RectTransform close = Rect("Close", frame, new Vector2(0.42f, 0.02f), new Vector2(0.58f, 0.1f));
            close.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.3f, 0.4f, 1f);
            Button button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = close.GetComponent<Image>();
            Label("Close", close, Vector2.zero, Vector2.one);
            UnityEventTools.AddPersistentListener(button.onClick, panel.Close);
            ChestInventoryPresenter presenter = chest.AddComponent<ChestInventoryPresenter>();
            ChestCreation.SetObject(presenter, "panel", panel);
            canvasObject.SetActive(false);
        }

        private static InventoryView CreateView(string title, Transform parent, GameObject slotTemplate, Vector2 minimum, Vector2 maximum)
        {
            RectTransform root = Rect(title + " Inventory", parent, minimum, maximum);
            InventoryView view = root.gameObject.AddComponent<InventoryView>();
            Label(title, root, new Vector2(0f, 0.91f), Vector2.one);
            TMP_Text count = Label("", root, Vector2.zero, new Vector2(1f, 0.07f));
            RectTransform viewport = Rect("Viewport", root, new Vector2(0f, 0.08f), new Vector2(1f, 0.9f));
            viewport.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform content = Rect("Slots", viewport, new Vector2(0f, 1f), Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(90f, 72f);
            grid.spacing = new Vector2(5f, 5f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            ChestCreation.SetObject(view, "slotPrefab", slotTemplate);
            ChestCreation.SetObject(view, "gridContainer", content);
            ChestCreation.SetObject(view, "slotCountText", count);
            return view;
        }

        private static GameObject CreateSlotTemplate(Transform parent)
        {
            RectTransform root = Rect("Slot Template", parent);
            root.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.23f, 0.3f, 1f);
            InventorySlotView slot = root.gameObject.AddComponent<InventorySlotView>();
            TMP_Text name = Label("", root, new Vector2(0.04f, 0.35f), new Vector2(0.96f, 0.96f));
            TMP_Text quantity = Label("", root, new Vector2(0.5f, 0.03f), new Vector2(0.96f, 0.35f));
            ChestCreation.SetObject(slot, "itemNameText", name);
            ChestCreation.SetObject(slot, "quantityText", quantity);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static TMP_Text Label(string text, Transform parent, Vector2 minimum, Vector2 maximum)
        {
            RectTransform rect = Rect(string.IsNullOrEmpty(text) ? "Text" : text, parent, minimum, maximum);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 18f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
            => Rect(name, parent, Vector2.zero, Vector2.one);

        private static RectTransform Rect(string name, Transform parent, Vector2 minimum, Vector2 maximum)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
