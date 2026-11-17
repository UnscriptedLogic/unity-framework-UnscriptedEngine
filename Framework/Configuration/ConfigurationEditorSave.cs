#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnscriptedEngine.Configuration
{
    /// <summary>Queues supported scene-instance fields for restoration after Play Mode ends.</summary>
    [InitializeOnLoad]
    public static class ConfigurationEditorSave
    {
        private const string StateKey = "UnscriptedEngine.ConfigurationEditorSave";

        [Serializable]
        private sealed class ComponentAddress
        {
            public string globalId;
        }

        [Serializable]
        private sealed class SavedField
        {
            public string componentId;
            public string declaringType;
            public string name;
            public string json;
        }

        [Serializable]
        private sealed class SaveState
        {
            public List<ComponentAddress> components = new List<ComponentAddress>();
            public List<SavedField> fields = new List<SavedField>();
        }

        static ConfigurationEditorSave()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Snapshots all supported fields. Repeated saves replace previous snapshots per field.</summary>
        public static bool TrySave(ConfigurableObject target, out string message)
        {
            if (!EditorApplication.isPlaying || target == null)
            {
                message = "Save to Editor requires a target in Play Mode.";
                return false;
            }

            SaveState state = ReadState();
            List<SavedField> snapshot = new List<SavedField>();
            foreach (ConfigurableField field in target.Fields)
            {
                if (field.Component == null) continue;
                string globalId = GlobalObjectId.GetGlobalObjectIdSlow(field.Component).ToString();
                ComponentAddress address = state.components.Find(item => item.globalId == globalId);
                if (address == null)
                {
                    message = "Cannot save runtime-created components. Use an object from a saved scene.";
                    return false;
                }
                object value = field.Read();
                if (!field.Definition.TryNormalize(value, out object normalized))
                {
                    message = "Cannot save invalid value: " + field.Field.Name;
                    return false;
                }
                snapshot.Add(new SavedField
                {
                    componentId = address.globalId,
                    declaringType = field.Field.DeclaringType.FullName,
                    name = field.Path,
                    json = field.Definition.SerializeValue(normalized)
                });
            }
            if (snapshot.Count == 0)
            {
                message = "No supported fields to save.";
                return false;
            }
            foreach (SavedField field in snapshot)
            {
                state.fields.RemoveAll(item => item.componentId == field.componentId
                    && item.declaringType == field.declaringType && item.name == field.name);
                state.fields.Add(field);
            }
            WriteState(state);
            message = "Saved " + snapshot.Count + " fields. Applied to scene after Play Mode; then save scene.";
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) CaptureSceneAddresses();
            if (change == PlayModeStateChange.EnteredEditMode) ApplySavedFields();
        }

        private static void CaptureSceneAddresses()
        {
            SaveState state = new SaveState();
            HashSet<string> seen = new HashSet<string>();
            foreach (ConfigurableObject target in UnityEngine.Object.FindObjectsByType<ConfigurableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(target.gameObject.scene.path)) continue;
                foreach (ConfigurableField field in target.Fields)
                {
                    MonoBehaviour component = field.Component;
                    if (component == null || string.IsNullOrEmpty(component.gameObject.scene.path)) continue;
                    string globalId = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
                    if (!seen.Add(globalId)) continue;
                    state.components.Add(new ComponentAddress
                    {
                        globalId = globalId
                    });
                }
            }
            WriteState(state);
        }

        private static void ApplySavedFields()
        {
            SaveState state = ReadState();
            SessionState.EraseString(StateKey);
            if (state.fields.Count == 0) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Save configuration from Play Mode");
            int applied = 0;
            foreach (SavedField saved in state.fields)
            {
                if (!GlobalObjectId.TryParse(saved.componentId, out GlobalObjectId id)) continue;
                MonoBehaviour component = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as MonoBehaviour;
                ConfigurableObject target = component != null ? component.GetComponentInParent<ConfigurableObject>() : null;
                ConfigurableField field = target != null ? FindField(target, component, saved) : null;
                if (field == null || !field.Definition.TryDeserializeValue(saved.json, out object value))
                {
                    Debug.LogWarning("Saved configuration field could not be restored: " + saved.name);
                    continue;
                }
                Undo.RecordObject(component, "Save configuration from Play Mode");
                if (!field.TryWriteSerialized(value, out string reason))
                {
                    Debug.LogWarning("Saved configuration field could not be restored: " + saved.name + ": " + reason);
                    continue;
                }
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                applied++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Restored " + applied + "/" + state.fields.Count + " configuration fields from Play Mode. Save scene to keep them.");
        }

        private static ConfigurableField FindField(ConfigurableObject target, MonoBehaviour component, SavedField saved)
        {
            foreach (ConfigurableField field in target.Fields)
                if (field.Component == component && field.Path == saved.name
                    && field.Field.DeclaringType.FullName == saved.declaringType) return field;
            return null;
        }

        private static SaveState ReadState() => JsonUtility.FromJson<SaveState>(SessionState.GetString(StateKey, "{}")) ?? new SaveState();
        private static void WriteState(SaveState state) => SessionState.SetString(StateKey, JsonUtility.ToJson(state));
    }
}
#endif
