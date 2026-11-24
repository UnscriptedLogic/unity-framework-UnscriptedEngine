using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    public sealed class FavouritesWindow : EditorWindow
    {
        private sealed class Entry
        {
            internal string Guid;
            internal string Path;
            internal Object Asset;
            internal GUIContent Label;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private string search = "";
        private Vector2 scroll;
        private Object dragAsset;
        private Vector2 dragOrigin;

        [MenuItem("Tools/UnscriptedEngine/Favourites")]
        public static void Open() => GetWindow<FavouritesWindow>("Favourites");

        private void OnEnable()
        {
            minSize = new Vector2(250f, 180f);
            EditorApplication.projectChanged += RefreshEntries;
            RefreshEntries();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshEntries;
            dragAsset = null;
        }

        private void RefreshEntries()
        {
            entries.Clear();
            foreach (string guid in FavouritesStore.instance.AssetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
                string name = asset == null ? "Missing asset (" + guid + ")" : Path.GetFileNameWithoutExtension(path);
                if (asset != null && AssetDatabase.IsValidFolder(path)) name = Path.GetFileName(path);
                entries.Add(new Entry
                {
                    Guid = guid,
                    Path = path,
                    Asset = asset,
                    Label = new GUIContent(name, asset == null ? null : AssetDatabase.GetCachedIcon(path), path)
                });
            }
            Repaint();
        }

        private static bool IsSupported(Object asset)
        {
            return asset != null && EditorUtility.IsPersistent(asset) && AssetDatabase.IsMainAsset(asset);
        }

        private void AddAssets(IEnumerable<Object> assets)
        {
            FavouritesStore.instance.Add(assets.Where(IsSupported)
                .Select(asset => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))));
            RefreshEntries();
        }

        private void HandleDrop()
        {
            Event current = Event.current;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;
            bool supported = DragAndDrop.objectReferences.Any(IsSupported);
            DragAndDrop.visualMode = supported ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            if (supported && current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddAssets(DragAndDrop.objectReferences);
                dragAsset = null;
            }
            current.Use();
        }

        private void OnGUI()
        {
            HandleDrop();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button(new GUIContent("Add Selected", "Add selected files and folders from the Project window."), EditorStyles.toolbarButton))
                    AddAssets(Selection.objects);
            }
            EditorGUILayout.HelpBox("Drop files or folders from the Project window here. Click to find an entry in Project. Double-click a prefab to open it. Drag entries into compatible fields or the Scene view. Saved per project in UserSettings.", MessageType.None);

            string removeGuid = null;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Entry entry in entries)
            {
                if (!string.IsNullOrEmpty(search) && entry.Label.text.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0
                    && entry.Path.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect row = GUILayoutUtility.GetRect(entry.Label, EditorStyles.label, GUILayout.Height(22f), GUILayout.ExpandWidth(true));
                    GUI.Label(row, entry.Label);
                    HandleEntry(row, entry);
                    if (GUILayout.Button(new GUIContent("×", "Remove favourite. Does not delete the asset."), GUILayout.Width(24f)))
                        removeGuid = entry.Guid;
                }
            }
            if (entries.Count == 0) EditorGUILayout.LabelField("No favourites yet. Drag in a Project file or folder.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
            if (removeGuid != null)
            {
                FavouritesStore.instance.Remove(removeGuid);
                RefreshEntries();
            }
            if (Event.current.rawType == EventType.MouseUp) dragAsset = null;
        }

        private void HandleEntry(Rect row, Entry entry)
        {
            Event current = Event.current;
            if (entry.Asset == null) return;
            if (current.type == EventType.MouseDown && current.button == 0 && row.Contains(current.mousePosition))
            {
                dragAsset = entry.Asset;
                dragOrigin = current.mousePosition;
                Selection.activeObject = entry.Asset;
                EditorGUIUtility.PingObject(entry.Asset);
                if (current.clickCount == 2 && entry.Asset is GameObject) AssetDatabase.OpenAsset(entry.Asset);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && dragAsset == entry.Asset
                && (current.mousePosition - dragOrigin).sqrMagnitude > 16f)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { entry.Asset };
                DragAndDrop.paths = new[] { entry.Path };
                DragAndDrop.StartDrag(entry.Label.text);
                dragAsset = null;
                current.Use();
            }
        }
    }
}
