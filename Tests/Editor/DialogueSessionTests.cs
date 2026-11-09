using NUnit.Framework;
using UnityEngine;
using UnscriptedEngine.Dialogue;

namespace UnscriptedEngine.Tests
{
    public sealed class DialogueSessionTests
    {
        private DialogueDefinition definition;

        [TearDown]
        public void Cleanup() { if (definition != null) Object.DestroyImmediate(definition); }

        [Test]
        public void SessionsSharingAssetKeepIndependentProgress()
        {
            definition = DialogueDefinition.CreateRuntime(new DialogueLine("npc", "First"), new DialogueLine("npc", "Second"));
            DialogueSession first = new DialogueSession(definition);
            DialogueSession second = new DialogueSession(definition);
            first.Start();
            second.Start();
            first.Advance();
            Assert.That(first.LineIndex, Is.EqualTo(1));
            Assert.That(second.LineIndex, Is.Zero);
            Assert.That(definition.Lines[0].Text, Is.EqualTo("First"));
        }

        [Test]
        public void ChoicesRequireSelectionAndFollowTheirDestination()
        {
            definition = DialogueDefinition.CreateRuntime(
                new DialogueLine("npc", "Choose", -2, new DialogueChoice("Branch", 2), new DialogueChoice("Exit", -1)),
                new DialogueLine("npc", "Skipped"), new DialogueLine("npc", "Branch", -1));
            DialogueSession session = new DialogueSession(definition);
            bool? wasCancelled = null;
            session.Ended += cancelled => wasCancelled = cancelled;
            session.Start();
            Assert.That(session.Advance(), Is.False);
            Assert.That(session.Choose(-1), Is.False);
            Assert.That(session.Choose(2), Is.False);
            Assert.That(session.Choose(0), Is.True);
            Assert.That(session.CurrentLine.Text, Is.EqualTo("Branch"));
            session.Advance();
            Assert.That(session.IsRunning, Is.False);
            Assert.That(wasCancelled, Is.False);
        }

        [Test]
        public void InvalidDestinationRejectedBeforeConversationStarts()
        {
            definition = DialogueDefinition.CreateRuntime(new DialogueLine("npc", "Broken", 10));
            Assert.Throws<System.ArgumentException>(() => new DialogueSession(definition));
            Object.DestroyImmediate(definition);
            definition = DialogueDefinition.CreateRuntime(new DialogueLine("npc", "Broken", -2, new DialogueChoice("Broken", -2)));
            Assert.Throws<System.ArgumentException>(() => new DialogueSession(definition));
        }

        [Test]
        public void CancellationNotifiesOnceAndPreventsFurtherProgress()
        {
            definition = DialogueDefinition.CreateRuntime(new DialogueLine("npc", "Hello"));
            DialogueSession session = new DialogueSession(definition);
            int endCount = 0;
            session.Ended += cancelled => { Assert.That(cancelled, Is.True); endCount++; };
            session.Start();
            session.Cancel();
            session.Cancel();
            Assert.That(endCount, Is.EqualTo(1));
            Assert.That(session.Advance(), Is.False);
            Assert.That(session.Start(), Is.False);
            Assert.That(session.CurrentLine, Is.Null);
        }

        [Test]
        public void ReentrantAdvanceCannotSkipLinesDuringPresentation()
        {
            definition = DialogueDefinition.CreateRuntime(new DialogueLine("npc", "First"), new DialogueLine("npc", "Second"));
            DialogueSession session = new DialogueSession(definition);
            session.LineChanged += line => Assert.That(session.Advance(), Is.False);
            session.Start();
            Assert.That(session.LineIndex, Is.Zero);
            session.Advance();
            Assert.That(session.LineIndex, Is.EqualTo(1));
            session.Advance();
            Assert.That(session.IsRunning, Is.False);
        }
    }
}
