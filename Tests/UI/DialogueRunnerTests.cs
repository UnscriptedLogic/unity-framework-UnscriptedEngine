using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UnscriptedEngine.Dialogue;
using UnscriptedEngine.UI.Dialogue;

namespace UnscriptedEngine.Tests
{
    public sealed class DialogueRunnerTests
    {
        private GameObject root;
        private DialogueDefinition definition;
        private ScreenDialoguePresenter presenter;
        private DialogueSpeaker speaker;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return new EnterPlayMode();
            root = new GameObject("Test root");
            GameObject view = Child("View", root.transform);
            view.AddComponent<Canvas>();
            presenter = view.AddComponent<ScreenDialoguePresenter>();
            GameObject content = Child("Content", view.transform);
            TMP_Text label = Child("Line", content.transform).AddComponent<TextMeshProUGUI>();
            Button choice = Child("Choice", content.transform).AddComponent<Button>();
            Child("Choice label", choice.transform).AddComponent<TextMeshProUGUI>();
            presenter.Configure(content, label, null, choice, null);
            speaker = Child("Speaker", root.transform).AddComponent<DialogueSpeaker>();
            speaker.Initialize("npc", "NPC");
            definition = DialogueDefinition.CreateRuntime(new DialogueLine("npc", "Hello"), new DialogueLine("npc", "Goodbye"));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition);
            yield return new ExitPlayMode();
        }

        private static GameObject Child(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private DialogueRunner Runner(string name)
        {
            DialogueRunner runner = Child(name, root.transform).AddComponent<DialogueRunner>();
            runner.Configure(definition, presenter, new DialogueSpeaker[] { speaker });
            return runner;
        }

        [Test]
        public void AdvanceRevealsBeforeChangingLine()
        {
            DialogueRunner runner = Runner("Runner");
            Assert.That(runner.TryPlay(), Is.True);
            runner.Advance();
            Assert.That(presenter.IsRevealed, Is.True);
            Assert.That(runner.Session.LineIndex, Is.Zero);
            runner.Advance();
            Assert.That(runner.Session.LineIndex, Is.EqualTo(1));
        }

        [Test]
        public void PresenterHasExclusiveOwnerUntilCancellation()
        {
            DialogueRunner first = Runner("First");
            DialogueRunner second = Runner("Second");
            Assert.That(first.TryPlay(), Is.True);
            Assert.That(second.TryPlay(), Is.False);
            first.Cancel();
            Assert.That(second.TryPlay(), Is.True);
        }

        [Test]
        public void DisablingPresenterCancelsAndReleasesItForReplay()
        {
            DialogueRunner runner = Runner("Runner");
            runner.Play();
            presenter.gameObject.SetActive(false);
            Assert.That(runner.IsRunning, Is.False);
            presenter.gameObject.SetActive(true);
            Assert.That(runner.TryPlay(), Is.True);
            runner.enabled = false;
            Assert.That(runner.IsRunning, Is.False);
        }

        [Test]
        public void ModalCancellationReleasesFocusWithoutReleasingOtherOwners()
        {
            DialogueRunner runner = Runner("Runner");
            runner.Configure(definition, presenter, new DialogueSpeaker[] { speaker }, modal: true);
            using (System.IDisposable otherFocus = InputFocus.Acquire(root))
            {
                runner.Play();
                runner.Cancel();
                Assert.That(InputFocus.IsBlocked(null), Is.True);
            }
            Assert.That(InputFocus.IsBlocked(null), Is.False);
        }
    }
}
