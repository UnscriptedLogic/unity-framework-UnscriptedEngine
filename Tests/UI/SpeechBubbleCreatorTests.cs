using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Dialogue;
using UnscriptedEngine.UI.Dialogue;
using UnscriptedEngine.UI.Editor;

namespace UnscriptedEngine.Tests
{
    public sealed class SpeechBubbleCreatorTests
    {
        [Test]
        public void CreatorPreservesExistingComponentsAndWiringSurvivesPrefabSaveAndUndo()
        {
            const string folder = "Assets/SpeechBubbleCreatorTests";
            AssetDatabase.CreateFolder("Assets", "SpeechBubbleCreatorTests");
            GameObject pawn = new GameObject("Existing merchant");
            GameObject loadedPrefab = null;
            try
            {
                UInteractableComponent interaction = pawn.AddComponent<UInteractableComponent>();
                DialogueSpeaker speaker = pawn.AddComponent<DialogueSpeaker>();
                GameObject anchor = new GameObject("Existing head anchor");
                anchor.transform.SetParent(pawn.transform, false);
                speaker.Initialize("merchant", "Existing name", anchor.transform);
                SpeechBubbleInteraction speech = OverheadSpeechBubbleCreator.Create(pawn, "Hello", folder + "/Greeting.asset");
                Assert.That(pawn.GetComponent<UInteractableComponent>(), Is.SameAs(interaction));
                Assert.That(pawn.GetComponent<DialogueSpeaker>(), Is.SameAs(speaker));
                Assert.That(speaker.HeadAnchor, Is.SameAs(anchor.transform));
                Assert.That(speaker.SpeakerId, Is.EqualTo("merchant"));
                Assert.That(speaker.DisplayName, Is.EqualTo("Existing name"));

                PrefabUtility.SaveAsPrefabAsset(pawn, folder + "/Merchant.prefab");
                loadedPrefab = PrefabUtility.LoadPrefabContents(folder + "/Merchant.prefab");
                SpeechBubbleInteraction savedSpeech = loadedPrefab.GetComponent<SpeechBubbleInteraction>();
                Assert.That(savedSpeech.Runner, Is.Not.Null);
                SerializedObject runnerSettings = new SerializedObject(savedSpeech.Runner);
                Assert.That(runnerSettings.FindProperty("conversation").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<DialogueDefinition>(folder + "/Greeting.asset")));
                Assert.That(runnerSettings.FindProperty("automaticAdvance").boolValue, Is.True);
                Assert.That(runnerSettings.FindProperty("blockGameplay").boolValue, Is.False);
                WorldDialoguePresenter savedPresenter = loadedPrefab.GetComponentInChildren<WorldDialoguePresenter>();
                Assert.That(savedPresenter.IsConfigured, Is.True);

                Undo.PerformUndo();
                Assert.That(pawn.GetComponent<SpeechBubbleInteraction>(), Is.Null);
                Assert.That(pawn.GetComponent<UInteractableComponent>(), Is.SameAs(interaction));
                Assert.That(pawn.GetComponent<DialogueSpeaker>(), Is.SameAs(speaker));
                Assert.That(pawn.transform.childCount, Is.EqualTo(1));
                Assert.That(AssetDatabase.LoadAssetAtPath<DialogueDefinition>(folder + "/Greeting.asset"), Is.Not.Null);
                Undo.PerformRedo();
                Assert.That(pawn.GetComponent<SpeechBubbleInteraction>().Runner, Is.Not.Null);
            }
            finally
            {
                if (loadedPrefab != null) PrefabUtility.UnloadPrefabContents(loadedPrefab);
                Object.DestroyImmediate(pawn);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
