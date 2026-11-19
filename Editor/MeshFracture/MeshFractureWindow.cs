using System;
using System.Collections.Generic;
using System.IO;
using Framework.Generators;
using UnscriptedEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Framework.Editor
{
    public sealed class MeshFractureWindow : EditorWindow
    {
        [SerializeField, HideInInspector] private MeshFilter sourceObject;
        [SerializeField] private GameObject sourceRoot;
        private const int AllSolids = 0;
        private const int CustomSelection = 1;
        private const int FirstSolidOption = 2;

        [SerializeField] private int sourcePart;
        [SerializeField] private List<int> selectedParts = new();
        [SerializeField] private Mesh sourceAsset;
        [SerializeField] private FractureSettings settings = new FractureSettings();
        [SerializeField] private Material interiorMaterial;
        private enum ColliderShape { Box, Sphere, Mesh }

        [SerializeField, FormerlySerializedAs("addBoxColliders")] private bool addColliders;
        [SerializeField] private ColliderShape colliderShape;
        [SerializeField] private bool addRigidbodies;
        [SerializeField] private bool sleepUntilInteraction;
        [SerializeField] private bool wakeOnPhysics = true;
        [SerializeField] private float minimumImpactSpeed = RigidbodyImpactRelease.DefaultMinimumImpactSpeed;
        [SerializeField] private bool deactivateSource = true;
        [SerializeField] private float explode;
        private readonly List<(MeshFractureInput Input, Renderer Renderer)> sourceEntries = new();
        private Material[][] previewExterior;
        private MeshFracture.Piece[] pieces;
        private PreviewRenderUtility preview;
        private Bounds bounds;
        private Vector2 orbit = new Vector2(35, 25);
        private Vector2 scroll;
        private Vector2 partsScroll;
        private string error;

        [MenuItem("Tools/UnscriptedEngine/Procedural/Mesh Fracture")]
        private static void Open()
        {
            var window = GetWindow<MeshFractureWindow>("Mesh Fracture");
            if (Selection.activeGameObject != null)
            {
                window.sourceRoot = Selection.activeGameObject;
                window.sourcePart = AllSolids;
                window.selectedParts.Clear();
            }
            if (Selection.activeObject is Mesh mesh)
            {
                window.sourceRoot = null;
                window.sourcePart = AllSolids;
                window.selectedParts.Clear();
                window.sourceAsset = mesh;
            }
            window.ClearPieces();
        }

        private void OnEnable()
        {
            if (sourceRoot == null && sourceObject != null)
                sourceRoot = sourceObject.gameObject;
            sourceObject = null;
        }

        private void OnDisable()
        {
            ClearPieces();
            preview?.Cleanup();
            preview = null;
        }

        private bool WaitForInteraction => addRigidbodies && addColliders && sleepUntilInteraction;

        private string SourceName => sourcePart >= FirstSolidOption && sourcePart - FirstSolidOption < sourceEntries.Count
            ? sourceEntries[sourcePart - FirstSolidOption].Input.Name
            : sourceRoot != null ? sourceRoot.name : sourceAsset != null ? sourceAsset.name : "Mesh";

        private void CollectSources()
        {
            sourceEntries.Clear();
            if (sourceRoot == null)
            {
                if (sourceAsset != null)
                    sourceEntries.Add((new MeshFractureInput(sourceAsset, Matrix4x4.identity, sourceAsset.name), null));
                return;
            }

            Matrix4x4 worldToRoot = sourceRoot.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in sourceRoot.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;

                Matrix4x4 toRoot = filter.gameObject == sourceRoot
                    ? Matrix4x4.identity : worldToRoot * filter.transform.localToWorldMatrix;
                Renderer renderer = filter.GetComponent<Renderer>();
                if (filter.TryGetComponent(out IMeshFractureSource provider))
                {
                    foreach (MeshFractureInput input in provider.GetFractureInputs())
                    {
                        string label = $"{filter.name} · {input.Name ?? input.Mesh?.name}";
                        sourceEntries.Add((input.TransformBy(toRoot, label), renderer));
                    }
                }
                else
                {
                    sourceEntries.Add((new MeshFractureInput(filter.sharedMesh, toRoot, filter.name), renderer));
                }
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            GameObject previousRoot = sourceRoot;
            sourceRoot = (GameObject)EditorGUILayout.ObjectField("Source Object", sourceRoot, typeof(GameObject), true);
            using (new EditorGUI.DisabledScope(sourceRoot != null))
                sourceAsset = (Mesh)EditorGUILayout.ObjectField("Mesh Asset", sourceAsset, typeof(Mesh), false);
            if (previousRoot != sourceRoot)
            {
                sourcePart = AllSolids;
                selectedParts.Clear();
            }

            CollectSources();
            DrawSourceSelection();

            settings.NoiseTexture = (Texture2D)EditorGUILayout.ObjectField("Noise Texture", settings.NoiseTexture, typeof(Texture2D), false);
            settings.NoiseStrength = EditorGUILayout.FloatField("Noise Strength", settings.NoiseStrength);
            settings.NoiseScale = EditorGUILayout.FloatField("Noise Scale", settings.NoiseScale);
            settings.MinPieceSize = EditorGUILayout.FloatField("Min Piece Size", settings.MinPieceSize);
            settings.MaxPieceSize = EditorGUILayout.FloatField("Max Piece Size", settings.MaxPieceSize);
            settings.MaxPieces = Mathf.Clamp(EditorGUILayout.IntField("Max Pieces", settings.MaxPieces),
                1, FractureSettings.PieceLimit);
            settings.Seed = EditorGUILayout.IntField("Seed", settings.Seed);
            if (EditorGUI.EndChangeCheck())
                ClearPieces();

            interiorMaterial = (Material)EditorGUILayout.ObjectField("Interior Material", interiorMaterial, typeof(Material), false);
            addColliders = EditorGUILayout.Toggle("Add Colliders", addColliders);
            using (new EditorGUI.DisabledScope(!addColliders))
                colliderShape = (ColliderShape)EditorGUILayout.EnumPopup("Collider Shape", colliderShape);
            addRigidbodies = EditorGUILayout.Toggle("Add Rigidbodies", addRigidbodies);
            using (new EditorGUI.DisabledScope(!addRigidbodies || !addColliders))
                sleepUntilInteraction = EditorGUILayout.Toggle(new GUIContent("Sleep Until Interaction",
                    "Keep pieces fixed until the player uses the fractured object. Requires colliders and rigidbodies."), sleepUntilInteraction);
            if (WaitForInteraction)
            {
                wakeOnPhysics = EditorGUILayout.Toggle(new GUIContent("Wake On Physics",
                    "Release the pieces when hit by a fast Rigidbody or targeted by PhysicsForceUtility."), wakeOnPhysics);
                using (new EditorGUI.DisabledScope(!wakeOnPhysics))
                {
                    float speed = EditorGUILayout.FloatField(new GUIContent("Minimum Impact Speed",
                        "Minimum speed into the surface, in metres per second. Applies to collisions."), minimumImpactSpeed);
                    minimumImpactSpeed = float.IsFinite(speed) ? Mathf.Max(0.01f, speed) :
                        RigidbodyImpactRelease.DefaultMinimumImpactSpeed;
                }
            }

            bool hasSceneSource = sourceRoot != null && sourceRoot.scene.IsValid() && sourcePart == AllSolids;
            using (new EditorGUI.DisabledScope(!hasSceneSource))
            {
                deactivateSource = EditorGUILayout.Toggle(new GUIContent("Deactivate Source",
                    "Applies when creating scene objects. Undo restores the source object."), deactivateSource);
            }
            if (sourcePart != AllSolids)
                EditorGUILayout.HelpBox("Partial selections keep the source object active. Bake a prefab to place the pieces separately.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(sourceEntries.Count == 0 || (sourcePart == CustomSelection && selectedParts.Count == 0)))
                {
                    if (GUILayout.Button("Preview"))
                        Generate();
                }
                using (new EditorGUI.DisabledScope(pieces == null))
                {
                    if (GUILayout.Button("Create in Scene"))
                        CreateInScene();
                    if (GUILayout.Button("Bake Prefab"))
                        Bake();
                }
            }

            if (!string.IsNullOrEmpty(error))
                EditorGUILayout.HelpBox(error, MessageType.Error);
            if (pieces != null)
            {
                int outsideRange = 0;
                foreach (MeshFracture.Piece piece in pieces)
                {
                    if (piece.Size > settings.MaxPieceSize || piece.Size < settings.MinPieceSize)
                        outsideRange++;
                }
                EditorGUILayout.LabelField("Pieces", pieces.Length.ToString());
                if (outsideRange > 0)
                    EditorGUILayout.HelpBox($"{outsideRange} piece(s) outside the requested size range.", MessageType.Info);
                explode = EditorGUILayout.Slider("Explode", explode, 0, 1);
                Rect rect = GUILayoutUtility.GetRect(10, 320, GUILayout.ExpandWidth(true));
                DrawPreview(rect);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSelection()
        {
            if (sourceEntries.Count <= 1)
            {
                sourcePart = AllSolids;
                return;
            }

            sourcePart = Mathf.Clamp(sourcePart, AllSolids, sourceEntries.Count + FirstSolidOption - 1);
            var labels = new string[sourceEntries.Count + FirstSolidOption];
            labels[AllSolids] = $"All Solids ({sourceEntries.Count})";
            labels[CustomSelection] = "Select Multiple";
            for (int i = 0; i < sourceEntries.Count; i++)
                labels[i + FirstSolidOption] = sourceEntries[i].Input.Name;
            sourcePart = EditorGUILayout.Popup("Fracture Source", sourcePart, labels);
            if (sourcePart != CustomSelection)
                return;

            selectedParts.RemoveAll(index => index >= sourceEntries.Count);
            partsScroll = EditorGUILayout.BeginScrollView(partsScroll, GUILayout.MaxHeight(160));
            for (int i = 0; i < sourceEntries.Count; i++)
            {
                bool selected = selectedParts.Contains(i);
                if (EditorGUILayout.ToggleLeft(sourceEntries[i].Input.Name, selected) == selected)
                    continue;

                if (selected)
                    selectedParts.Remove(i);
                else
                    selectedParts.Add(i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Generate()
        {
            ClearPieces();
            try
            {
                CollectSources();
                var inputs = new List<MeshFractureInput>();
                var exterior = new List<Material[]>();
                for (int i = 0; i < sourceEntries.Count; i++)
                {
                    bool selected = sourcePart == AllSolids ||
                        (sourcePart == CustomSelection ? selectedParts.Contains(i) : sourcePart - FirstSolidOption == i);
                    if (!selected)
                        continue;

                    (MeshFractureInput input, Renderer renderer) = sourceEntries[i];
                    inputs.Add(input);
                    exterior.Add(renderer != null ? renderer.sharedMaterials : Array.Empty<Material>());
                }
                previewExterior = exterior.ToArray();

                pieces = MeshFracture.GenerateSolids(inputs, settings);
                bounds = GetPieceBounds(pieces[0]);
                foreach (MeshFracture.Piece piece in pieces)
                    bounds.Encapsulate(GetPieceBounds(piece));
            }
            catch (Exception exception)
            {
                ClearPieces();
                error = exception.Message;
            }
        }

        private static Bounds GetPieceBounds(MeshFracture.Piece piece)
        {
            Bounds pieceBounds = piece.Mesh.bounds;
            pieceBounds.center += piece.LocalPosition;
            return pieceBounds;
        }

        private Material[][] GetMaterials()
        {
            Material fallback = GraphicsSettings.currentRenderPipeline?.defaultMaterial;
            if (fallback == null)
                fallback = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            var bySource = new Material[previewExterior.Length][];
            foreach (MeshFracture.Piece piece in pieces)
            {
                int source = piece.SourceIndex;
                if (bySource[source] != null)
                    continue;

                var materials = new Material[piece.Mesh.subMeshCount];
                Material[] exterior = previewExterior[source];
                for (int i = 0; i < materials.Length - 1; i++)
                    materials[i] = i < exterior.Length && exterior[i] != null ? exterior[i] : fallback;
                materials[materials.Length - 1] = interiorMaterial != null ? interiorMaterial : fallback;
                bySource[source] = materials;
            }
            return bySource;
        }

        private void DrawPreview(Rect rect)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDrag && current.button == 0 && rect.Contains(current.mousePosition))
            {
                orbit.x += current.delta.x;
                orbit.y = Mathf.Clamp(orbit.y + current.delta.y, -89, 89);
                current.Use();
                Repaint();
            }
            if (current.type != EventType.Repaint)
                return;

            preview ??= new PreviewRenderUtility();
            preview.camera.allowHDR = false;
            preview.camera.allowMSAA = false;
            preview.camera.fieldOfView = 35;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, 40, 0);
            preview.lights[1].intensity = 0.6f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340, 220, 0);
            preview.ambientColor = Color.gray;
            float size = bounds.size.magnitude;
            float aspect = Mathf.Max(0.15f, rect.width / rect.height);
            float distance = size * (1.9f + explode) / Mathf.Min(1, aspect);
            Quaternion rotation = Quaternion.Euler(orbit.y, orbit.x, 0);
            preview.camera.transform.SetPositionAndRotation(rotation * (Vector3.back * distance), rotation);
            preview.camera.nearClipPlane = distance * 0.01f;
            preview.camera.farClipPlane = distance * 3;

            Material[][] materials = GetMaterials();
            preview.BeginPreview(rect, GUIStyle.none);
            foreach (MeshFracture.Piece piece in pieces)
            {
                Vector3 offset = (piece.LocalPosition - bounds.center) * (1 + explode);
                Material[] pieceMaterials = materials[piece.SourceIndex];
                for (int i = 0; i < pieceMaterials.Length; i++)
                    preview.DrawMesh(piece.Mesh, offset, Quaternion.identity, pieceMaterials[i], i);
            }
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void Bake()
        {
            string path = EditorUtility.SaveFilePanelInProject("Bake Fracture Prefab",
                SourceName + " Fractured", "prefab", "Choose a destination in Assets.");
            if (!string.IsNullOrEmpty(path))
                BakeTo(path);
        }

        private void BakeTo(string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(path, ".asset"));
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var copies = new Mesh[pieces.Length];
            bool createdMeshAsset = false;
            try
            {
                Material[][] materials = GetMaterials();
                for (int i = 0; i < pieces.Length; i++)
                {
                    MeshFracture.Piece piece = pieces[i];
                    Mesh mesh = Instantiate(piece.Mesh);
                    mesh.name = piece.Mesh.name;
                    copies[i] = mesh;
                    if (i == 0)
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                        createdMeshAsset = true;
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(mesh, meshPath);
                    }

                    CreatePiece(root.transform, piece, mesh, materials[piece.SourceIndex]);
                }

                ConfigureInteraction(root, useUndo: false);
                AssetDatabase.SaveAssets();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                    throw new InvalidOperationException("Unity could not save the fracture prefab.");
                error = null;
                EditorGUIUtility.PingObject(prefab);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (createdMeshAsset)
                    AssetDatabase.DeleteAsset(meshPath);
            }
            finally
            {
                DestroyImmediate(root);
                foreach (Mesh mesh in copies)
                {
                    if (mesh != null && !EditorUtility.IsPersistent(mesh))
                        DestroyImmediate(mesh);
                }
            }
        }

        private void CreateInScene()
        {
            const string undoName = "Create Fractured Mesh";
            Transform sourceTransform = sourceRoot != null && sourceRoot.scene.IsValid()
                ? sourceRoot.transform : null;
            Scene scene = sourceTransform != null ? sourceTransform.gameObject.scene : SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
            {
                error = "Open a scene to create fracture pieces.";
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            try
            {
                var root = new GameObject(SourceName + " Fractured");
                if (root.scene != scene)
                    SceneManager.MoveGameObjectToScene(root, scene);

                if (sourceTransform != null)
                {
                    root.transform.SetParent(sourceTransform.parent, false);
                    root.transform.localPosition = sourceTransform.localPosition;
                    root.transform.localRotation = sourceTransform.localRotation;
                    root.transform.localScale = sourceTransform.localScale;
                }

                Undo.RegisterCreatedObjectUndo(root, undoName);
                Material[][] materials = GetMaterials();
                foreach (MeshFracture.Piece piece in pieces)
                {
                    // Scene objects own independent copies which serialize with the scene.
                    Mesh mesh = Instantiate(piece.Mesh);
                    mesh.name = piece.Mesh.name;
                    Undo.RegisterCreatedObjectUndo(mesh, undoName);
                    GameObject child = CreatePiece(root.transform, piece, mesh, materials[piece.SourceIndex]);
                    Undo.RegisterCreatedObjectUndo(child, undoName);
                }

                ConfigureInteraction(root, useUndo: true);
                if (sourceTransform != null && deactivateSource && sourcePart == AllSolids)
                {
                    Undo.RecordObject(sourceTransform.gameObject, undoName);
                    sourceTransform.gameObject.SetActive(false);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = root;
                error = null;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                error = exception.Message;
            }
        }

        private GameObject CreatePiece(Transform parent, MeshFracture.Piece piece, Mesh mesh, Material[] materials)
        {
            var child = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(parent, false);
            child.transform.localPosition = piece.LocalPosition;
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            child.GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (addColliders)
                AddPieceCollider(child, mesh);
            if (addRigidbodies)
                child.AddComponent<Rigidbody>().isKinematic = WaitForInteraction;
            return child;
        }

        private void ConfigureInteraction(GameObject root, bool useUndo)
        {
            if (!WaitForInteraction)
                return;

            RigidbodyReleaseGroup group = AddComponent<RigidbodyReleaseGroup>(root, useUndo);
            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>();
            using (var groupSettings = new SerializedObject(group))
            {
                SerializedProperty references = groupSettings.FindProperty("bodies");
                references.arraySize = bodies.Length;
                for (int i = 0; i < bodies.Length; i++)
                    references.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
                ApplySettings(groupSettings, useUndo);
            }

            FractureInteraction interaction = AddComponent<FractureInteraction>(root, useUndo);
            using (var interactionSettings = new SerializedObject(interaction))
            {
                interactionSettings.FindProperty("releaseGroup").objectReferenceValue = group;
                ApplySettings(interactionSettings, useUndo);
            }

            if (!wakeOnPhysics)
                return;

            foreach (Rigidbody body in bodies)
            {
                RigidbodyImpactRelease trigger = AddComponent<RigidbodyImpactRelease>(body.gameObject, useUndo);
                using var triggerSettings = new SerializedObject(trigger);
                triggerSettings.FindProperty("group").objectReferenceValue = group;
                triggerSettings.FindProperty("minimumImpactSpeed").floatValue = minimumImpactSpeed;
                ApplySettings(triggerSettings, useUndo);
            }
        }

        private static T AddComponent<T>(GameObject target, bool useUndo) where T : Component =>
            useUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();

        private static void ApplySettings(SerializedObject settings, bool useUndo)
        {
            if (useUndo)
                settings.ApplyModifiedProperties();
            else
                settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private void AddPieceCollider(GameObject piece, Mesh mesh)
        {
            Bounds meshBounds = mesh.bounds;
            switch (colliderShape)
            {
                case ColliderShape.Box:
                    BoxCollider box = piece.AddComponent<BoxCollider>();
                    box.center = meshBounds.center;
                    box.size = meshBounds.size;
                    break;
                case ColliderShape.Sphere:
                    SphereCollider sphere = piece.AddComponent<SphereCollider>();
                    sphere.center = meshBounds.center;
                    float radiusSquared = 0;
                    foreach (Vector3 vertex in mesh.vertices)
                        radiusSquared = Mathf.Max(radiusSquared, (vertex - sphere.center).sqrMagnitude);
                    sphere.radius = Mathf.Sqrt(radiusSquared);
                    break;
                case ColliderShape.Mesh:
                    MeshCollider meshCollider = piece.AddComponent<MeshCollider>();
                    meshCollider.convex = addRigidbodies;
                    meshCollider.sharedMesh = mesh;
                    break;
            }
        }

        private void ClearPieces()
        {
            error = null;
            previewExterior = null;
            if (pieces == null)
                return;
            foreach (MeshFracture.Piece piece in pieces)
                DestroyImmediate(piece.Mesh);
            pieces = null;
        }
    }
}
