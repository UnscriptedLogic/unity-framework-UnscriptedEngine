using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Dialogue;
using UnscriptedEngine.UI.Dialogue;

namespace UnscriptedEngine.UI.Editor
{
    public sealed class OverheadSpeechBubbleCreator : EditorWindow
    {
        [SerializeField]
        [Tooltip("Scene pawn receiving an independent speech interaction. Existing shop behavior is preserved.")]
        private GameObject target;
        [SerializeField]
        [Tooltip("Optional head anchor belonging to the pawn. Empty reuses the speaker's anchor or creates one above its renderers.")]
        private Transform headAnchor;
        [SerializeField]
        [TextArea(2, 6)]
        [Tooltip("Initial greeting. Additional lines can be authored in the generated conversation asset.")]
        private string greeting = "Welcome! Take a look at my wares.";

        [MenuItem("Tools/UnscriptedEngine/Dialogue/Create Overhead Speech Bubble")]
        public static void ShowWindow()
        {
            OverheadSpeechBubbleCreator window = GetWindow<OverheadSpeechBubbleCreator>("Overhead Speech Bubble");
            window.target = Selection.activeGameObject;
            window.minSize = new Vector2(400f, 240f);
        }

        private void OnGUI()
        {
            SerializedObject settings = new SerializedObject(this);
            settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("target"));
            EditorGUILayout.PropertyField(settings.FindProperty("headAnchor"));
            EditorGUILayout.PropertyField(settings.FindProperty("greeting"));
            settings.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Adds a timed speech bubble beside existing shop or quest behavior. Successful local interaction shows the greeting without blocking gameplay. Scene changes support Undo; the saved greeting asset remains reusable.", MessageType.Info);
            string problem = GetTargetProblem(target, headAnchor);
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(problem != null || string.IsNullOrWhiteSpace(greeting) || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (!GUILayout.Button("Create and Wire Speech Bubble")) return;
                string folder = AssetDatabase.IsValidFolder("Assets/Prototypes/Dialogue") ? "Assets/Prototypes/Dialogue" : "Assets";
                string assetPath = EditorUtility.SaveFilePanelInProject("Save greeting conversation", "Greeting", "asset", "Choose a new asset path for this pawn's greeting.", folder);
                if (string.IsNullOrEmpty(assetPath)) return;
                try
                {
                    SpeechBubbleInteraction speech = Create(target, greeting, assetPath, headAnchor);
                    Selection.activeGameObject = speech.gameObject;
                    EditorGUIUtility.PingObject(speech.Runner);
                }
                catch (Exception exception) { EditorUtility.DisplayDialog("Cannot create speech bubble", exception.Message, "OK"); }
            }
        }

        private static string GetTargetProblem(GameObject pawn, Transform anchor)
        {
            if (pawn == null || EditorUtility.IsPersistent(pawn) || !pawn.scene.IsValid() || !pawn.scene.isLoaded)
                return "Select a scene pawn, or open its prefab in Prefab Mode.";
            if (pawn.GetComponent<SpeechBubbleInteraction>() != null)
                return "This pawn already has a SpeechBubbleInteraction. Edit its runner and conversation instead.";
            if (anchor != null && anchor != pawn.transform && !anchor.IsChildOf(pawn.transform))
                return "Head anchor must belong to the selected pawn.";
            DialogueSpeaker speaker = pawn.GetComponent<DialogueSpeaker>();
            if (speaker != null && string.IsNullOrWhiteSpace(speaker.SpeakerId))
                return "Existing DialogueSpeaker needs a non-empty Speaker ID.";
            return null;
        }

        /// <summary>Creates a saved greeting and undoable scene setup. Never replaces existing assets or behaviors.</summary>
        public static SpeechBubbleInteraction Create(GameObject pawn, string text, string assetPath, Transform anchor = null)
        {
            string problem = GetTargetProblem(pawn, anchor);
            if (problem != null) throw new ArgumentException(problem, nameof(pawn));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Greeting text is required.", nameof(text));
            string normalizedPath = string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) || normalizedPath.Contains("..") ||
                !normalizedPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/')) ||
                File.Exists(normalizedPath))
                throw new ArgumentException("Choose an unused .asset path inside an existing Assets folder.", nameof(assetPath));

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create overhead speech bubble");
            DialogueDefinition definition = null;
            bool assetCreated = false;
            try
            {
                DialogueSpeaker speaker = pawn.GetComponent<DialogueSpeaker>();
                bool newSpeaker = speaker == null;
                if (newSpeaker) speaker = Undo.AddComponent<DialogueSpeaker>(pawn);
                SerializedObject speakerSettings = new SerializedObject(speaker);
                if (anchor == null) anchor = speakerSettings.FindProperty("headAnchor").objectReferenceValue as Transform;
                if (anchor == null)
                {
                    GameObject anchorObject = CreateObject("Speech Anchor", pawn.transform);
                    anchor = anchorObject.transform;
                    Renderer[] renderers = pawn.GetComponentsInChildren<Renderer>(true);
                    Bounds bounds = new Bounds(pawn.transform.position + Vector3.up, Vector3.zero);
                    bool hasBounds = false;
                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer.GetComponentInParent<Canvas>() != null) continue;
                        if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    anchor.position = hasBounds ? new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z)
                        : pawn.transform.position + Vector3.up * 2f;
                }
                if (newSpeaker)
                {
                    speakerSettings.FindProperty("speakerId").stringValue = "speaker";
                    speakerSettings.FindProperty("displayName").stringValue = pawn.name;
                }
                speakerSettings.FindProperty("headAnchor").objectReferenceValue = anchor;
                speakerSettings.ApplyModifiedProperties();

                definition = DialogueDefinition.CreateRuntime(new DialogueLine(speaker.SpeakerId, text));
                AssetDatabase.CreateAsset(definition, normalizedPath);
                assetCreated = true;
                GameObject bubble = CreateObject("Overhead Speech Bubble", pawn.transform, typeof(RectTransform), typeof(Canvas));
                Canvas canvas = bubble.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform bubbleRect = (RectTransform)bubble.transform;
                bubbleRect.sizeDelta = new Vector2(480f, 180f);
                bubbleRect.pivot = new Vector2(0.5f, 0f);
                bubbleRect.localScale = Vector3.one * 0.003f;
                bubbleRect.position = anchor.position;
                WorldDialoguePresenter presenter = Undo.AddComponent<WorldDialoguePresenter>(bubble);
                GameObject panel = CreateObject("Content", bubble.transform, typeof(RectTransform), typeof(Image));
                Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.one);
                Image background = panel.GetComponent<Image>();
                background.color = new Color(0.045f, 0.055f, 0.08f, 0.96f);
                background.raycastTarget = false;
                GameObject tail = CreateObject("Tail", panel.transform, typeof(RectTransform), typeof(Image));
                RectTransform tailRect = (RectTransform)tail.transform;
                tailRect.anchorMin = tailRect.anchorMax = new Vector2(0.5f, 0f);
                tailRect.sizeDelta = new Vector2(22f, 22f);
                tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                tail.GetComponent<Image>().color = background.color;
                tail.GetComponent<Image>().raycastTarget = false;
                TMP_Text nameLabel = CreateLabel(panel.transform, "Speaker", new Vector2(0f, 0.75f), Vector2.one, 22f);
                nameLabel.text = speaker.DisplayName;
                nameLabel.fontStyle = FontStyles.Bold;
                TMP_Text lineLabel = CreateLabel(panel.transform, "Line", Vector2.zero, new Vector2(1f, 0.75f), 26f);
                lineLabel.text = text;
                presenter.Configure(panel, lineLabel, nameLabel, null, null);

                GameObject runnerObject = CreateObject("Speech Runner", pawn.transform);
                DialogueRunner runner = Undo.AddComponent<DialogueRunner>(runnerObject);
                runner.Configure(definition, presenter, new DialogueSpeaker[] { speaker }, timed: true);
                UInteractableComponent interaction = pawn.GetComponent<UInteractableComponent>();
                if (interaction == null) interaction = Undo.AddComponent<UInteractableComponent>(pawn);
                SpeechBubbleInteraction speech = Undo.AddComponent<SpeechBubbleInteraction>(pawn);
                speech.Configure(interaction, runner);
                EditorUtility.SetDirty(presenter);
                EditorUtility.SetDirty(runner);
                EditorUtility.SetDirty(speech);
                PrefabUtility.RecordPrefabInstancePropertyModifications(speaker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(speech);
                Undo.CollapseUndoOperations(undoGroup);
                return speech;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (assetCreated) AssetDatabase.DeleteAsset(normalizedPath);
                else if (definition != null) DestroyImmediate(definition);
                throw;
            }
        }

        private static GameObject CreateObject(string name, Transform parent, params Type[] components)
        {
            GameObject root = new GameObject(name, components);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            Undo.SetTransformParent(root.transform, parent, "Parent " + name);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(16f, 8f);
            rect.offsetMax = new Vector2(-16f, -8f);
        }

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 min, Vector2 max, float size)
        {
            GameObject root = CreateObject(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
            Stretch((RectTransform)root.transform, min, max);
            TMP_Text label = root.GetComponent<TMP_Text>();
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
