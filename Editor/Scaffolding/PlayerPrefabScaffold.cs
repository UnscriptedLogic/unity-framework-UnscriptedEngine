using System;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public static class PlayerPrefabScaffold
    {
        public static void Validate(IPlayerPrefabScaffoldTemplate template)
        {
            ValidateSource(template.ControllerPrefab, template.ControllerBaseType, typeof(UController));
            ValidateSource(template.PawnPrefab, template.PawnBaseType, typeof(UPawn));
        }

        private static void ValidateSource(GameObject prefab, Type baseType, Type frameworkBase)
        {
            if (baseType == null || baseType.IsSealed || baseType.IsGenericType || !baseType.IsVisible
                || !frameworkBase.IsAssignableFrom(baseType))
                throw new InvalidOperationException("Preset must use an inheritable " + frameworkBase.Name + " type.");
            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab) || prefab.transform.parent != null
                || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponents(frameworkBase).Length != 1
                || prefab.GetComponent(frameworkBase).GetType() != baseType)
                throw new InvalidOperationException("Preset requires a prefab root with NetworkObject and exactly one " + baseType.Name + ".");
        }

        public static GameObject Clone(GameObject source, Type baseType, Type generatedType, string name, Scene scene)
        {
            if (generatedType == null || generatedType.IsAbstract || !baseType.IsAssignableFrom(generatedType))
                throw new InvalidOperationException("Generated or skipped script must inherit " + baseType.Name + ". Choose Replace to regenerate it.");
            var root = Object.Instantiate(source);
            try
            {
                root.name = name;
                SceneManager.MoveGameObjectToScene(root, scene);
                var original = root.GetComponent(baseType);
                var replacement = root.AddComponent(generatedType);
                // Copy inherited serialized fields, including references already remapped into this clone.
                EditorUtility.CopySerializedManagedFieldsOnly(original, replacement);
                if (original is Behaviour oldBehaviour && replacement is Behaviour newBehaviour)
                    newBehaviour.enabled = oldBehaviour.enabled;

                // Components anywhere in the hierarchy may reference the original controller/pawn.
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component == original) continue;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference
                            && property.objectReferenceValue == original)
                            property.objectReferenceValue = replacement;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                Object.DestroyImmediate(original);
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }
    }
}
