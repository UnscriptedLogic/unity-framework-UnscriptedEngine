# if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine
{
    public static class UCreateMenuExtensions
    {
        [MenuItem("Assets/UnscriptedEngine/Create/Game Mode Manager", priority = 40)]
        public static void CreateGameModeManagerPrefab()
        {
            GameObject emptyPrefab = new GameObject();

            // Prompt the user to enter a name in the Project window
            string prefabPath = $"{GetSelectedPathOrFallback()}/{emptyPrefab.name}.prefab";
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                emptyPrefab.GetInstanceID(),
                ScriptableObject.CreateInstance<EndNameEditAction>(),
                prefabPath,
                null,
                null
            );
        }

        public static string GetSelectedPathOrFallback()
        {
            string path = "Assets";

            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }

        private class EndNameEditAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                // If the user entered a name, save the GameObject as a prefab
                GameObject emptyPrefab = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                Debug.Log(emptyPrefab.name);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(emptyPrefab, pathName);

                AttachScript(prefab.name, prefab);

                DestroyImmediate(emptyPrefab);

                Debug.Log("Prefab created with script: " + pathName);
            }

            private void AttachScript(string scriptName, GameObject gameObject)
            {
                // Define the script template
                string scriptTemplate = $"using UnityEngine;\nusing UnscriptedEngine;\n\npublic class {scriptName} : UGameModeBase\n{{\n    // Your script code here\n}}";

                // Get the path to the scripts folder
                string scriptsFolder = "Assets/Scripts/";

                // Create the script file
                System.IO.File.WriteAllText($"{scriptsFolder}O_{scriptName}.cs", scriptTemplate);

                // Refresh the AssetDatabase to make sure the new script is recognized
                AssetDatabase.Refresh();

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{scriptsFolder}GM_{scriptName}.cs");

                prefab.AddComponent(System.Type.GetType($"O_{scriptName}"));
            }
        }
    }
}


#endif