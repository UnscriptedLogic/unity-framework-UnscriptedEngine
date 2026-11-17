using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class ObjectMotionTests
    {
        private GameObject root;
        [SetUp] public void SetUp() => root = new GameObject("Motion fixture");
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void PathStopsOnceAndReplayKeepsOriginalOrigin()
        {
            root.transform.position = Vector3.right * 10;
            var motion = root.AddComponent<ObjectTranslaterComponent>();
            motion.Points = new[] { Vector3.right, Vector3.right * 3 };
            int completed = 0;
            motion.Completed.AddListener(() => completed++);
            motion.Play(); motion.Advance(2);
            Assert.That(root.transform.position.x, Is.EqualTo(12).Within(0.001));
            motion.Advance(10); motion.Advance(10);
            Assert.That(root.transform.position.x, Is.EqualTo(13).Within(0.001));
            Assert.That(completed, Is.EqualTo(1));
            motion.Play(); motion.Advance(0.5f);
            Assert.That(root.transform.position.x, Is.EqualTo(10.5f).Within(0.001));
            motion.ResetPose();
            Assert.That(root.transform.position.x, Is.EqualTo(10));
        }

        [Test]
        public void ReturnToOriginVisitsOriginBetweenPoints()
        {
            var motion = root.AddComponent<ObjectTranslaterComponent>();
            motion.Traversal = ObjectPointTraversal.ReturnToOrigin;
            motion.Points = new[] { Vector3.right, Vector3.up };
            motion.Play(); motion.Advance(2);
            Assert.That(root.transform.position.magnitude, Is.LessThan(0.001));
            motion.Advance(0.5f);
            Assert.That(Vector3.Distance(root.transform.position, Vector3.up * 0.5f), Is.LessThan(0.001));
            motion.Advance(1.5f);
            Assert.That(motion.IsPlaying, Is.False);
            Assert.That(root.transform.position.magnitude, Is.LessThan(0.001));
        }

        [TestCase(ObjectMotionCompletion.Restart, 0.25f)]
        [TestCase(ObjectMotionCompletion.PingPong, 0.75f)]
        public void LoopModesPreserveOvershoot(ObjectMotionCompletion completion, float expected)
        {
            var motion = root.AddComponent<ObjectTranslaterComponent>();
            motion.Points = new[] { Vector3.right };
            motion.Completion = completion;
            motion.Play(); motion.Advance(1.25f);
            Assert.That(root.transform.position.x, Is.EqualTo(expected).Within(0.001));
            Assert.That(motion.IsPlaying, Is.True);
            motion.enabled = false;
            Assert.That(motion.IsPlaying, Is.False);
        }

        [Test]
        public void FullAnglesPreservesTurnsAndShortestUsesQuaternionArc()
        {
            var motion = root.AddComponent<ObjectRotaterComponent>();
            motion.Points = new[] { Vector3.up * 450 };
            motion.Speed = 90;
            motion.RotationRoute = ObjectRotationRoute.FullAngles;
            motion.Play(); motion.Advance(2);
            Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, 180, 0)), Is.LessThan(0.01));
            Assert.That(motion.IsPlaying, Is.True);
            motion.ResetPose();
            motion.RotationRoute = ObjectRotationRoute.Shortest;
            motion.Play(); motion.Advance(1.1f);
            Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(0.01));
            Assert.That(motion.IsPlaying, Is.False);
        }

        [Test]
        public void ScaleWeightsLockAndSlowAxesAndThresholdClamps()
        {
            var motion = root.AddComponent<ObjectScalerComponent>();
            motion.AxisScaling = new Vector3(0, 0.5f, 1);
            motion.MaximumThreshold = 2;
            motion.Points = new[] { Vector3.one * 4 };
            motion.Play(); motion.Advance(1);
            Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(1, 1.5f, 2)));
            motion.Advance(10);
            Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(1, 2, 2)));
            motion.ResetPose();
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator KinematicMovementUsesFixedDispatcher()
        {
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var motion = root.AddComponent<ObjectTranslaterComponent>();
            motion.MotionMode = ObjectMotionMode.PhysicsKinematic;
            motion.Points = new[] { Vector3.right * 10 };
            motion.Play();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            motion.Stop();
            Assert.That(body.position.x, Is.GreaterThan(0));
            Assert.That(body.position.x, Is.LessThan(10));
            motion.ResetPose();
            Assert.That(body.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void KinematicModeRejectsDynamicBody()
        {
            root.AddComponent<Rigidbody>();
            var motion = root.AddComponent<ObjectTranslaterComponent>();
            motion.MotionMode = ObjectMotionMode.PhysicsKinematic;
            LogAssert.Expect(LogType.Error, "Kinematic object motion requires a kinematic Rigidbody on the same object.");
            motion.Play();
            Assert.That(motion.IsPlaying, Is.False);
        }

        [Test]
        public void CoincidentLoopTerminatesAndInvalidTimeDoesNotChangePose()
        {
            var motion = root.AddComponent<ObjectTranslaterComponent>();
            motion.Points = new[] { Vector3.zero };
            motion.Completion = ObjectMotionCompletion.Restart;
            motion.Play(); motion.Advance(1);
            Assert.That(motion.IsPlaying, Is.False);
            motion.Points = new[] { Vector3.right };
            motion.Play(); motion.Advance(float.NaN); motion.Advance(-1);
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
        }
    }
}

