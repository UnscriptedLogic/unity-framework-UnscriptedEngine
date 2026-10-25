using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnscriptedEngine.Editor.Scaffolding
{
    [InitializeOnLoad]
    public static class ItemScaffoldGeneration
    {
        private const string PendingKey = "UnscriptedEngine.ItemScaffold.Pending";
        private const string StatusKey = "UnscriptedEngine.ItemScaffold.Status";
        public static bool HasPending => !string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""));
        public static string Status => SessionState.GetString(StatusKey, "");

        static ItemScaffoldGeneration() { ScheduleCompletion(); }

        private static void ScheduleCompletion()
        {
            EditorApplication.update -= Resume;
            EditorApplication.update += Resume;
        }

        private static void Resume()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Resume;
            TryComplete();
        }

        public static void Start(ItemScaffoldPlan plan)
        {
            if (HasPending) throw new InvalidOperationException("Finish or cancel the pending item first.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Wait for Edit Mode and compilation to finish.");
            plan.ValidateNew();
            var sources = ItemScaffoldSources.Create(plan);
            plan.EnsureFolders();
            using (var files = new ScaffoldFileTransaction())
            {
                foreach (var source in sources)
                {
                    files.Capture(new ScaffoldPlan(), source.Key);
                    using var stream = new FileStream(source.Key, FileMode.CreateNew, FileAccess.Write);
                    using var writer = new StreamWriter(stream);
                    writer.Write(source.Value);
                }
                SessionState.SetString(PendingKey, JsonUtility.ToJson(plan));
                SessionState.SetString(StatusKey, "Compiling item scripts. Prefabs and item data will be created automatically.");
                files.Commit();
            }
            AssetDatabase.Refresh();
            ScheduleCompletion();
        }

        public static void TryComplete()
        {
            if (!HasPending || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (EditorUtility.scriptCompilationFailed)
                {
                    SessionState.SetString(StatusKey, "Fix Console compilation errors, then click Finish Pending Item.");
                    return;
                }
                var plan = JsonUtility.FromJson<ItemScaffoldPlan>(SessionState.GetString(PendingKey, ""));
                plan.Validate();
                // Resolve only from the generated source paths; never attach a same-named unrelated type.
                Type Resolve(string suffix)
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(plan.ScriptPath(suffix));
                    if (script == null) return null;
                    string assemblyName = Path.GetFileNameWithoutExtension(
                        CompilationPipeline.GetAssemblyNameFromScriptPath(plan.ScriptPath(suffix)));
                    return AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == assemblyName)?.GetType(plan.TypeName(suffix));
                }
                if (ItemScaffoldPlan.ScriptSuffixes.Any(suffix => Resolve(suffix) == null))
                {
                    SessionState.SetString(StatusKey, "Waiting for compiled item scripts. Fix Console errors, then click Finish Pending Item. If using an assembly definition, it must reference UnscriptedEngine.");
                    return;
                }
                var data = ItemScaffoldAssets.Build(plan, Resolve("Visual"), Resolve("WorldItem"), Resolve("Fragment"));
                SessionState.EraseString(PendingKey);
                SessionState.SetString(StatusKey, "Created " + plan.name + ". Item data and both prefabs are ready.");
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
                Debug.Log("UnscriptedEngine created item at " + plan.DataPath, data);
            }
            catch (Exception exception)
            {
                SessionState.SetString(StatusKey, "Creation paused: " + exception.Message + " Fix the issue and retry. Generated scripts are retained.");
                Debug.LogException(exception);
            }
        }

        public static void Cancel()
        {
            SessionState.EraseString(PendingKey);
            SessionState.SetString(StatusKey, "Pending item cancelled. Generated scripts and folders were retained.");
        }
    }
}
