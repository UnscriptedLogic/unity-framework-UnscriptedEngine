using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnscriptedEngine.Gameplay;

namespace UnscriptedEngine.Editor
{
    public sealed class ColliderZoneCreatorWindow : EditorWindow
    {
        private enum ZoneShape { Box, Sphere, Capsule }
        private enum ZoneKind { Collider, Damage, Status, Wind, Suction, Knockback }
        private ZoneKind zoneKind;
        private ZoneShape zoneShape;
        private string zoneName = "Collider Zone";
        private bool isTrigger = true;
        private bool addRigidbody = true;
        private bool placeAtViewCenter = true;
        private float placementDistance = 10f;
        private Vector3 size = Vector3.one * 2f;
        private Vector3 center;
        private float radius = 1f;
        private float height = 2f;
        private GameObject templateObject;
        private ColliderZone template;
        private SerializedObject settings;
        private DamageZone damageTemplate;
        private StatusZone statusTemplate;
        private SerializedObject damageSettings;
        private SerializedObject statusSettings;
        private PhysicsForceZone[] physicsTemplates;
        private SerializedObject[] physicsSettings;
        private Vector2 scroll;

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Zones/Collider Zones")]
        public static void Open() => GetWindow<ColliderZoneCreatorWindow>("Collider Zones");

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Zones/Damage Zones")]
        public static void OpenDamage() => GetWindow<ColliderZoneCreatorWindow>("Collider Zones").SetZoneKind(ZoneKind.Damage);

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Zones/Status Zones")]
        public static void OpenStatus() => GetWindow<ColliderZoneCreatorWindow>("Collider Zones").SetZoneKind(ZoneKind.Status);

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Zones/Wind Zones")]
        public static void OpenWind() => GetWindow<ColliderZoneCreatorWindow>("Collider Zones").SetZoneKind(ZoneKind.Wind);

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Zones/Suction Zones")]
        public static void OpenSuction() => GetWindow<ColliderZoneCreatorWindow>("Collider Zones").SetZoneKind(ZoneKind.Suction);

        [MenuItem("Tools/UnscriptedEngine/Gameplay/Zones/Knockback Zones")]
        public static void OpenKnockback() => GetWindow<ColliderZoneCreatorWindow>("Collider Zones").SetZoneKind(ZoneKind.Knockback);

        private void SetZoneKind(ZoneKind kind)
        {
            if (zoneName == zoneKind + " Zone") zoneName = kind + " Zone";
            zoneKind = kind;
            Repaint();
        }

        private void OnEnable()
        {
            templateObject = new GameObject("Zone Settings") { hideFlags = HideFlags.HideAndDontSave };
            templateObject.SetActive(false);
            templateObject.AddComponent<BoxCollider>();
            template = templateObject.AddComponent<ColliderZone>();
            settings = new SerializedObject(template);
            damageTemplate = templateObject.AddComponent<DamageZone>();
            statusTemplate = templateObject.AddComponent<StatusZone>();
            damageSettings = new SerializedObject(damageTemplate);
            statusSettings = new SerializedObject(statusTemplate);
            physicsTemplates = new PhysicsForceZone[] {
                templateObject.AddComponent<UnscriptedEngine.Gameplay.WindZone>(),
                templateObject.AddComponent<SuctionZone>(), templateObject.AddComponent<KnockbackZone>() };
            physicsTemplates[2].RequireLineOfSight = false;
            physicsSettings = new SerializedObject[physicsTemplates.Length];
            for (int index = 0; index < physicsTemplates.Length; index++)
                physicsSettings[index] = new SerializedObject(physicsTemplates[index]);
        }

        private void OnDisable()
        {
            settings?.Dispose();
            damageSettings?.Dispose();
            statusSettings?.Dispose();
            if (physicsSettings != null)
                foreach (SerializedObject effectSettings in physicsSettings) effectSettings?.Dispose();
            if (templateObject != null) DestroyImmediate(templateObject);
        }

        private void OnGUI()
        {
            if (settings == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            ZoneKind selectedKind = (ZoneKind)EditorGUILayout.EnumPopup("Zone Type", zoneKind);
            if (selectedKind != zoneKind) SetZoneKind(selectedKind);
            EditorGUILayout.LabelField(zoneKind + " Zone", EditorStyles.boldLabel);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            EditorGUILayout.HelpBox(stage != null ? "Creates inside the open prefab. Selected prefab objects become the parent."
                : "Creates in the current scene. Selected scene objects become the parent.", MessageType.Info);
            zoneName = EditorGUILayout.TextField("Name", zoneName);
            placeAtViewCenter = EditorGUILayout.Toggle(new GUIContent("Place At View Center", "Place at the closest collider under the Scene view camera's center. Only objects in the current stage are considered."), placeAtViewCenter);
            if (placeAtViewCenter)
                placementDistance = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Empty Space Distance", "Distance ahead of the Scene view camera when no collider is hit."), placementDistance));
            zoneShape = (ZoneShape)EditorGUILayout.EnumPopup("Shape", zoneShape);
            isTrigger = EditorGUILayout.Toggle("Is Trigger", isTrigger);
            addRigidbody = EditorGUILayout.Toggle(new GUIContent("Kinematic Rigidbody", "Makes trigger callbacks work against static colliders. Collision zones still need a dynamic body on one side."), addRigidbody);
            if (!isTrigger)
                EditorGUILayout.HelpBox("Solid zones support framework CharacterMovementComponent hits automatically. Other CharacterController movers must forward OnControllerColliderHit with ColliderZone.ForwardControllerHit(hit). Ordinary Rigidbody collisions need a dynamic body on one side.", MessageType.Info);
            center = EditorGUILayout.Vector3Field("Center", center);
            if (zoneShape == ZoneShape.Box) size = EditorGUILayout.Vector3Field("Size", size);
            else
            {
                radius = Mathf.Max(0.001f, EditorGUILayout.FloatField("Radius", radius));
                if (zoneShape == ZoneShape.Capsule) height = Mathf.Max(radius * 2f, EditorGUILayout.FloatField("Height (Y)", height));
            }
            settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("filter"), true);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(settings.FindProperty("entered"));
            EditorGUILayout.PropertyField(settings.FindProperty("stayed"));
            EditorGUILayout.PropertyField(settings.FindProperty("exited"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(settings.FindProperty("showGizmos"));
            EditorGUILayout.PropertyField(settings.FindProperty("flashDuration"));
            settings.ApplyModifiedProperties();
            if (zoneKind >= ZoneKind.Wind) DrawEffectSettings(physicsSettings[(int)zoneKind - (int)ZoneKind.Wind]);
            if (zoneKind == ZoneKind.Damage) DrawEffectSettings(damageSettings);
            if (zoneKind == ZoneKind.Status)
            {
                DrawEffectSettings(statusSettings);
                if (GUILayout.Button("Create Poison Effect Asset...")) CreatePoisonAsset();
                if (statusTemplate.Effect != null)
                {
                    EditorGUILayout.LabelField("Status Definition", EditorStyles.boldLabel);
                    using (SerializedObject definitionSettings = new SerializedObject(statusTemplate.Effect)) DrawEffectSettings(definitionSettings);
                }
            }
            EditorGUILayout.HelpBox("Tags use GameplayTags on the contacting object or nearest parent. Empty lists allow everything. One local collider per zone. Use child zones for compound shapes.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrWhiteSpace(zoneName)))
                if (GUILayout.Button("Create " + zoneKind + " Zone", GUILayout.Height(30f))) CreateZone();
            EditorGUILayout.EndScrollView();
        }

        private void CreateZone()
        {
            Vector3 viewPosition = Vector3.zero;
            bool hasViewPosition = placeAtViewCenter && TryGetViewPlacement(out viewPosition);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Collider Zone");
            GameObject zoneObject = new GameObject(zoneName.Trim());
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create Collider Zone");
            StageUtility.PlaceGameObjectInCurrentStage(zoneObject);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            Transform parent = Selection.activeTransform;
            if (parent != null && (EditorUtility.IsPersistent(parent) ||
                StageUtility.GetStageHandle(parent.gameObject) != StageUtility.GetCurrentStageHandle())) parent = null;
            if (parent == null && stage != null) parent = stage.prefabContentsRoot.transform;
            if (parent != null)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(zoneObject, parent.gameObject.scene);
                Undo.SetTransformParent(zoneObject.transform, parent, "Parent Collider Zone");
                zoneObject.transform.localPosition = Vector3.zero;
                zoneObject.transform.localRotation = Quaternion.identity;
                zoneObject.transform.localScale = Vector3.one;
            }
            if (hasViewPosition) zoneObject.transform.position = viewPosition;
            Collider collider;
            if (zoneShape == ZoneShape.Sphere)
            {
                SphereCollider sphere = Undo.AddComponent<SphereCollider>(zoneObject);
                sphere.radius = radius; sphere.center = center; collider = sphere;
            }
            else if (zoneShape == ZoneShape.Capsule)
            {
                CapsuleCollider capsule = Undo.AddComponent<CapsuleCollider>(zoneObject);
                capsule.radius = radius; capsule.height = height; capsule.center = center; collider = capsule;
            }
            else
            {
                BoxCollider box = Undo.AddComponent<BoxCollider>(zoneObject);
                box.size = new Vector3(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y), Mathf.Max(0.001f, size.z));
                box.center = center; collider = box;
            }
            collider.isTrigger = isTrigger;
            if (addRigidbody)
            {
                Rigidbody body = Undo.AddComponent<Rigidbody>(zoneObject);
                body.isKinematic = true; body.useGravity = false;
            }
            ColliderZone zone = Undo.AddComponent<ColliderZone>(zoneObject);
            EditorUtility.CopySerialized(template, zone);
            if (zoneKind == ZoneKind.Damage)
                EditorUtility.CopySerialized(damageTemplate, Undo.AddComponent<DamageZone>(zoneObject));
            if (zoneKind == ZoneKind.Status)
                EditorUtility.CopySerialized(statusTemplate, Undo.AddComponent<StatusZone>(zoneObject));
            if (zoneKind >= ZoneKind.Wind)
            {
                PhysicsForceZone effectTemplate = physicsTemplates[(int)zoneKind - (int)ZoneKind.Wind];
                EditorUtility.CopySerialized(effectTemplate, Undo.AddComponent(zoneObject, effectTemplate.GetType()));
            }
            EditorSceneManager.MarkSceneDirty(zoneObject.scene);
            Selection.activeGameObject = zoneObject;
            Undo.CollapseUndoOperations(group);
        }

        private static void DrawEffectSettings(SerializedObject serialized)
        {
            serialized.Update();
            SerializedProperty property = serialized.GetIterator();
            bool children = true;
            while (property.NextVisible(children))
            {
                children = false;
                if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
            }
            serialized.ApplyModifiedProperties();
        }

        private void CreatePoisonAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Poison Effect", "Poison", "asset", "Choose where to save this reusable poison definition.");
            if (string.IsNullOrEmpty(path)) return;
            PoisonEffect poison = CreateInstance<PoisonEffect>();
            AssetDatabase.CreateAsset(poison, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RecordObject(statusTemplate, "Assign Poison Effect");
            statusTemplate.Effect = poison;
            EditorGUIUtility.PingObject(poison);
        }

        private bool TryGetViewPlacement(out Vector3 position)
        {
            position = Vector3.zero;
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null) return false;

            Camera camera = view.camera;
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            position = camera.transform.position + ray.direction * placementDistance;
            float nearest = camera.farClipPlane;
            // Per-collider queries keep prefab-stage placement isolated from the main scene.
            foreach (Collider collider in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Collider>())
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy ||
                    (camera.cullingMask & (1 << collider.gameObject.layer)) == 0) continue;
                if (!collider.Raycast(ray, out RaycastHit hit, nearest)) continue;
                nearest = hit.distance;
                position = hit.point;
            }
            return true;
        }
    }
}
