using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnscriptedEngine.Dialogue;
using UnscriptedEngine.UI.Dialogue;
using UnscriptedEngine.UI.Editor;

namespace UnscriptedEngine.Tests
{
    public sealed class SpeechBubbleInteractionTests
    {
        private const string Folder = "Assets/SpeechBubbleInteractionTests";
        private GameObject pawn;
        private GameObject buyer;
        private SpeechBubbleInteraction speech;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return new EnterPlayMode();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "SpeechBubbleInteractionTests");
            pawn = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pawn.name = "Shopkeeper";
            buyer = new GameObject("Buyer");
            speech = OverheadSpeechBubbleCreator.Create(pawn, "Welcome!", Folder + "/Greeting.asset");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (pawn != null) Object.DestroyImmediate(pawn);
            if (buyer != null) Object.DestroyImmediate(buyer);
            AssetDatabase.DeleteAsset(Folder);
            yield return new ExitPlayMode();
        }

        [Test]
        public void SuccessfulInteractionShowsBubbleAlongsideExistingBehavior()
        {
            UInteractableComponent interaction = pawn.GetComponent<UInteractableComponent>();
            int otherBehaviorCount = 0;
            interaction.OnButtonPressedServer += args => otherBehaviorCount++;
            Assert.That(interaction.TryBeginInteraction(buyer), Is.True);
            Assert.That(speech.Runner.IsRunning, Is.True);
            Assert.That(otherBehaviorCount, Is.EqualTo(1));
            Assert.That(speech.Runner.Session.CurrentLine.Text, Is.EqualTo("Welcome!"));
            Assert.That(InputFocus.IsBlocked(null), Is.False);
            Assert.That(pawn.GetComponentInChildren<WorldDialoguePresenter>().HasAnchor, Is.True);
            DialogueSession first = speech.Runner.Session;
            Assert.That(interaction.TryBeginInteraction(buyer), Is.True);
            Assert.That(speech.Runner.Session, Is.SameAs(first));
            speech.CancelSpeech();
            Assert.That(interaction.TryBeginInteraction(buyer), Is.True);
            Assert.That(speech.Runner.Session, Is.Not.SameAs(first));
        }

        [Test]
        public void DisableUnsubscribesAndOnlyCancelsOwnedSpeech()
        {
            UInteractableComponent interaction = pawn.GetComponent<UInteractableComponent>();
            interaction.TryBeginInteraction(buyer);
            speech.enabled = false;
            Assert.That(speech.Runner.IsRunning, Is.False);
            interaction.TryBeginInteraction(buyer);
            Assert.That(speech.Runner.IsRunning, Is.False);
            speech.enabled = true;
            interaction.TryBeginInteraction(buyer);
            Assert.That(speech.Runner.IsRunning, Is.True);
            speech.CancelSpeech();
            speech.Runner.Play();
            speech.enabled = false;
            Assert.That(speech.Runner.IsRunning, Is.True, "An independently started session must retain its lifetime.");
            speech.Runner.Cancel();
        }

        [Test]
        public void CreatorReusesSpeakerAndInteractableAndRejectsDuplicateSetup()
        {
            Assert.That(pawn.GetComponents<UInteractableComponent>().Length, Is.EqualTo(1));
            Assert.That(pawn.GetComponents<DialogueSpeaker>().Length, Is.EqualTo(1));
            Assert.That(pawn.GetComponent<DialogueSpeaker>().HeadAnchor.position.y, Is.GreaterThan(1f));
            Assert.Throws<System.ArgumentException>(() =>
                OverheadSpeechBubbleCreator.Create(pawn, "Another", Folder + "/Duplicate.asset"));
            Assert.That(AssetDatabase.LoadAssetAtPath<DialogueDefinition>(Folder + "/Duplicate.asset"), Is.Null);
        }
    }
}
