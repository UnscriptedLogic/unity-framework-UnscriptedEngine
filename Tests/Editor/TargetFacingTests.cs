using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class TargetFacingTests
    {
        [Test]
        public void FacingTurnsAtConfiguredSpeedAndReportsAlignment()
        {
            GameObject root = new GameObject("Facing test");
            try
            {
                FaceToTarget facing = root.AddComponent<FaceToTarget>();
                Assert.That(facing.Face(Vector3.right, 0.1f), Is.False);
                Assert.That(Quaternion.Angle(Quaternion.identity, root.transform.rotation), Is.EqualTo(36f).Within(0.01f));
                Assert.That(facing.Face(Vector3.right, 1f), Is.True);
                Assert.That(Vector3.Angle(root.transform.forward, Vector3.right), Is.LessThan(0.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void YawFacingIgnoresHeightAndDoesNotRotateOnZeroTimeOrCoincidentTarget()
        {
            GameObject root = new GameObject("Facing test");
            try
            {
                FaceToTarget facing = root.AddComponent<FaceToTarget>();
                Assert.That(facing.Face(Vector3.right * 2f + Vector3.up * 9f, 0f), Is.False);
                Assert.That(root.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(facing.Face(Vector3.zero, 1f), Is.False);
                Assert.That(facing.Face(Vector3.right * 2f + Vector3.up * 9f, 1f), Is.True);
                Assert.That(root.transform.forward.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FullRotationAlignsAnOffsetMuzzleWithTheAimPoint()
        {
            GameObject root = new GameObject("Facing test");
            try
            {
                FaceToTarget facing = root.AddComponent<FaceToTarget>();
                UnityEditor.SerializedObject settings = new UnityEditor.SerializedObject(facing);
                settings.FindProperty("yawOnly").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Transform muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0.1f, 0.6f, 0.63f);
                Vector3 point = new Vector3(1f, 0.6f, 3f);

                Assert.That(facing.Face(point, 1f, muzzle), Is.True);
                Assert.That(Vector3.Angle(muzzle.forward, point - muzzle.position), Is.LessThan(0.05f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SightRejectsWallsAcceptsTargetChildrenAndIgnoresTriggers()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject target = new GameObject("Target root");
            try
            {
                Vector3 origin = new Vector3(700f, 700f, 700f);
                wall.transform.position = origin + Vector3.forward * 2f;
                Physics.SyncTransforms();
                Assert.That(LineOfSight.IsClear(origin, origin + Vector3.forward * 4f, ~0), Is.False);
                wall.transform.SetParent(target.transform);
                Assert.That(LineOfSight.IsClear(origin, origin + Vector3.forward * 4f, ~0, target.transform), Is.True);
                wall.GetComponent<Collider>().isTrigger = true;
                Physics.SyncTransforms();
                Assert.That(LineOfSight.IsClear(origin, origin + Vector3.forward * 4f, ~0), Is.True);
            }
            finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(target); }
        }
    }
}
