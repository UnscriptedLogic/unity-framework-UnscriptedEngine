using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnscriptedEngine.Tests
{
    public class ObjectAnimationTests
    {
        private GameObject root;
        private ObjectAnimBrain brain;
        private ObjectAnimMove move;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Animation fixture");
            root.transform.localPosition = new Vector3(2, 3, 4);
            brain = root.AddComponent<ObjectAnimBrain>();
            brain.Duration = 1;
            move = root.AddComponent<ObjectAnimMove>();
            move.Offset = new Vector3(4, 0, 0);
            move.Shape = ObjectAnimShape.Tween;
            move.Curve = AnimationCurve.Linear(0, 0, 1, 1);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [UnityTest]
        public IEnumerator DispatcherHonorsPausedTimeAndUnscaledOptIn()
        {
            float originalTimeScale = Time.timeScale;
            int listeners = UpdateManager.UpdateListenerCount;
            try
            {
                Time.timeScale = 0;
                brain.Duration = 10;
                brain.Play();
                Assert.That(UpdateManager.UpdateListenerCount, Is.EqualTo(listeners + 1));
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(root.transform.localPosition.x, Is.EqualTo(2));
                brain.UseUnscaledTime = true;
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(root.transform.localPosition.x, Is.GreaterThan(2));
                brain.enabled = false;
                Assert.That(root.transform.localPosition.x, Is.EqualTo(2));
                Assert.That(UpdateManager.UpdateListenerCount, Is.EqualTo(listeners));
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [Test]
        public void ChannelsComposeAndReplayNeverAccumulates()
        {
            var rotate = root.AddComponent<ObjectAnimRotate>();
            rotate.Shape = ObjectAnimShape.Tween;
            rotate.Degrees = new Vector3(0, 90, 0);
            rotate.Curve = AnimationCurve.Linear(0, 0, 1, 1);
            var scale = root.AddComponent<ObjectAnimScale>();
            scale.Shape = ObjectAnimShape.Tween;
            scale.Multiplier = new Vector3(2, 3, 4);
            scale.Curve = AnimationCurve.Linear(0, 0, 1, 1);
            root.transform.localScale = new Vector3(2, 1, 3);
            for (int i = 0; i < 3; i++)
            {
                brain.Play();
                brain.Advance(0.5f);
                Assert.That(root.transform.localPosition.x, Is.EqualTo(4).Within(0.0001));
                Assert.That(Quaternion.Angle(root.transform.localRotation, Quaternion.Euler(0, 45, 0)), Is.LessThan(0.001));
                Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(3, 2, 7.5f)));
            }
            brain.ResetPose();
            Assert.That(root.transform.localPosition, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(2, 1, 3)));
        }

        [Test]
        public void StopHoldsResetRestoresAndDisableCancels()
        {
            brain.Play(); brain.Advance(0.5f); brain.Stop(); brain.Advance(1);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(4));
            brain.ResetPose();
            Assert.That(root.transform.localPosition.x, Is.EqualTo(2));
            brain.Play(); brain.Advance(0.5f); brain.enabled = false;
            Assert.That(brain.IsPlaying, Is.False);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(2));
        }

        [TestCase(ObjectAnimShape.Punch)]
        [TestCase(ObjectAnimShape.Wiggle)]
        public void PolishReturnsExactlyToRest(ObjectAnimShape shape)
        {
            move.Shape = shape;
            brain.Play(); brain.Advance(0.125f);
            Assert.That(root.transform.localPosition.x, Is.Not.EqualTo(2));
            brain.Advance(2);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(2));
            Assert.That(brain.IsPlaying, Is.False);
        }

        [Test]
        public void RepeatsHandleLargeStepsAndCompleteOnlyOnce()
        {
            int completions = 0;
            brain.Completed.AddListener(() => completions++);
            brain.Cycles = 3;
            brain.Play(); brain.Advance(2.5f);
            Assert.That(brain.IsPlaying, Is.True);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(4));
            brain.Advance(4); brain.Advance(4);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(6));
            Assert.That(completions, Is.EqualTo(1));
            brain.Cycles = 0;
            brain.Play(); brain.Advance(100.5f);
            Assert.That(brain.IsPlaying, Is.True);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(4));
        }

        [Test]
        public void ZeroDurationCompletesEvenWhenRepeatingAndCanReplayFromCallback()
        {
            brain.Duration = 0; brain.Cycles = 0;
            brain.Completed.AddListener(() => { brain.Duration = 1; brain.Play(); });
            brain.Play();
            Assert.That(brain.IsPlaying, Is.True);
            brain.Advance(0.5f);
            Assert.That(root.transform.localPosition.x, Is.EqualTo(4));
        }

        [Test]
        public void DisabledEffectsAreSkippedAndChangingTargetReleasesOldPose()
        {
            move.enabled = false;
            brain.Play();
            Assert.That(brain.IsPlaying, Is.False);
            move.enabled = true;
            brain.Play(); brain.Advance(0.5f);
            var child = new GameObject("Replacement target"); child.transform.SetParent(root.transform, false);
            move.Target = child.transform;
            Assert.That(root.transform.localPosition.x, Is.EqualTo(2));
            brain.Advance(0.25f);
            Assert.That(child.transform.localPosition.x, Is.EqualTo(0));
            brain.Play(); brain.Advance(0.5f);
            Assert.That(child.transform.localPosition.x, Is.EqualTo(2));
        }

        [Test]
        public void ExplicitChildTrackIsExclusiveAndOldBrainCannotResetNewOwner()
        {
            var child = new GameObject("Child"); child.transform.SetParent(root.transform, false);
            move.Target = child.transform;
            brain.SetComponents(move, move, null);
            var other = child.AddComponent<ObjectAnimBrain>(); other.Duration = 1;
            other.SetComponents(move);
            brain.Play(); other.Play();
            Assert.That(other.IsPlaying, Is.False);
            move.enabled = false; move.enabled = true;
            other.Play(); other.Advance(0.5f);
            brain.ResetPose();
            Assert.That(child.transform.localPosition.x, Is.EqualTo(2));
            other.ResetPose();
            Assert.That(child.transform.localPosition.x, Is.EqualTo(0));
        }
    }
}
