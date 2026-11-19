using System;
using System.IO;
using Framework.Generators;
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
        [SerializeField] private MeshFilter sourceObject;
        [SerializeField] private Mesh sourceAsset;
        [SerializeField] private FractureSettings settings = new FractureSettings();
        [SerializeField] private Material interiorMaterial;
        private enum ColliderShape { Box, Sphere, Mesh }

        [SerializeField, FormerlySerializedAs("addBoxColliders")] private bool addColliders;
        [SerializeField] private ColliderShape colliderShape;
        [SerializeField] private bool addRigidbodies;
        [SerializeField] private bool deactivateSource = true;
        [SerializeField] private float explode;
        private MeshFracture.Piece[] pieces;
        private PreviewRenderUtility preview;
        private Bounds bounds;
        private Vector2 orbit = new Vector2(35, 25);
        private Vector2 scroll;
        private string error;

        [MenuItem("Tools/UnscriptedEngine/Procedural/Mesh Fracture")]
        private static void Open()
        {
            var window = GetWindow<MeshFractureWindow>("Mesh Fracture");
            if (Selection.activeGameObject != null)
                window.sourceObject = Selection.activeGameObject.GetComponent<MeshFilter>();
            if (Selection.activeObject is Mesh mesh)
            {
                window.sourceObject = null;
                window.sourceAsset = mesh;
            }
            window.ClearPieces();
        }

        private void OnDisable()
        {
            ClearPieces();
            preview?.Cleanup();
            preview = null;
        }

        private Mesh Source => sourceObject != null ? sourceObject.sharedMesh : sourceAsset;

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            sourceObject = (MeshFilter)EditorGUILayout.ObjectField("Scene Mesh Filter", sourceObject, typeof(MeshFilter), true);
            using (new EditorGUI.DisabledScope(sourceObject != null))
                sourceAsset = (Mesh)EditorGUILayout.ObjectField("Mesh Asset", sourceAsset, typeof(Mesh), false);
            settings.NoiseTexture = (Texture2D)EditorGUILayout.ObjectField("Noise Texture", settings.NoiseTexture, typeof(Texture2D), false);
            settings.NoiseStrength = EditorGUILayout.FloatField("Noise Strength", settings.NoiseStrength);
            settings.NoiseScale = EditorGUILayout.FloatField("Noise Scale", settings.NoiseScale);
            settings.MinPieceSize = EditorGUILayout.FloatField("Min Piece Size", settings.MinPieceSize);
            settings.MaxPieceSize = EditorGUILayout.FloatField("Max Piece Size", settings.MaxPieceSize);
            settings.MaxPieces = EditorGUILayout.IntSlider("Max Pieces", settings.MaxPieces, 1, 128);
            settings.Seed = EditorGUILayout.IntField("Seed", settings.Seed);
            if (EditorGUI.EndChangeCheck())
                ClearPieces();

            interiorMaterial = (Material)EditorGUILayout.ObjectField("Interior Material", interiorMaterial, typeof(Material), false);
            addColliders = EditorGUILayout.Toggle("Add Colliders", addColliders);
            using (new EditorGUI.DisabledScope(!addColliders))
                colliderShape = (ColliderShape)EditorGUILayout.EnumPopup("Collider Shape", colliderShape);
            addRigidbodies = EditorGUILayout.Toggle("Add Rigidbodies", addRigidbodies);
            bool hasSceneSource = sourceObject != null && sourceObject.gameObject.scene.IsValid();
            using (new EditorGUI.DisabledScope(!hasSceneSource))
            {
                deactivateSource = EditorGUILayout.Toggle(new GUIContent("Deactivate Source",
                    "Applies when creating scene objects. Undo restores the source object."), deactivateSource);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(Source == null))
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

        private void Generate()
        {
            ClearPieces();
            Mesh mesh = Source;
            try
            {
                bounds = mesh.bounds;
                pieces = MeshFracture.Generate(mesh, settings);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
        }

        private Material[] GetMaterials(int submeshCount)
        {
            var materials = new Material[submeshCount];
            Material fallback = GraphicsSettings.currentRenderPipeline?.defaultMaterial;
            if (fallback == null)
                fallback = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            Renderer renderer = sourceObject != null ? sourceObject.GetComponent<Renderer>() : null;
            Material[] sourceMaterials = renderer != null ? renderer.sharedMaterials : Array.Empty<Material>();
            for (int i = 0; i < materials.Length - 1; i++)
                materials[i] = i < sourceMaterials.Length && sourceMaterials[i] != null ? sourceMaterials[i] : fallback;
            materials[materials.Length - 1] = interiorMaterial != null ? interiorMaterial : fallback;
            return materials;
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

            Material[] materials = GetMaterials(pieces[0].Mesh.subMeshCount);
            preview.BeginPreview(rect, GUIStyle.none);
            foreach (MeshFracture.Piece piece in pieces)
            {
                Vector3 offset = (piece.LocalPosition - bounds.center) * (1 + explode);
                for (int i = 0; i < materials.Length; i++)
                    preview.DrawMesh(piece.Mesh, offset, Quaternion.identity, materials[i], i);
            }
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void Bake()
        {
            string path = EditorUtility.SaveFilePanelInProject("Bake Fracture Prefab",
                Source.name + " Fractured", "prefab", "Choose a destination in Assets.");
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
                Material[] materials = GetMaterials(pieces[0].Mesh.subMeshCount);
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

                    CreatePiece(root.transform, piece, mesh, materials);
                }

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
            Transform sourceTransform = sourceObject != null && sourceObject.gameObject.scene.IsValid()
                ? sourceObject.transform : null;
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
                var root = new GameObject(Source.name + " Fractured");
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
                Material[] materials = GetMaterials(pieces[0].Mesh.subMeshCount);
                foreach (MeshFracture.Piece piece in pieces)
                {
                    // Scene objects own independent copies which serialize with the scene.
                    Mesh mesh = Instantiate(piece.Mesh);
                    mesh.name = piece.Mesh.name;
                    Undo.RegisterCreatedObjectUndo(mesh, undoName);
                    GameObject child = CreatePiece(root.transform, piece, mesh, materials);
                    Undo.RegisterCreatedObjectUndo(child, undoName);
                }

                if (sourceTransform != null && deactivateSource)
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
                child.AddComponent<Rigidbody>();
            return child;
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
            if (pieces == null)
                return;
            foreach (MeshFracture.Piece piece in pieces)
                DestroyImmediate(piece.Mesh);
            pieces = null;
        }
    }
}
