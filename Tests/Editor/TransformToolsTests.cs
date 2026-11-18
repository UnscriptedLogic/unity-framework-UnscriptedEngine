using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnscriptedEngine.Transforms;
using Object = UnityEngine.Object;

namespace UnscriptedEngine.Tests
{
    public sealed class TransformToolsTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private Transform Create(string name, Vector3 position)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position; objects.Add(root); return root.transform;
        }
        [UnitySetUp] public IEnumerator SetUp() { yield return new EnterPlayMode(); }
        [UnityTearDown] public IEnumerator TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--) if (objects[index] != null) Object.DestroyImmediate(objects[index]);
            objects.Clear(); yield return new ExitPlayMode();
        }

        [Test] public void SelectionUsesGroupCenterAndNeverContainsBothParentAndChild()
        {
            Transform first = Create("First", Vector3.left * 2f);
            Transform second = Create("Second", Vector3.right * 4f);
            Transform child = Create("Child", first.position); child.SetParent(first);
            TransformSelection selection = new TransformSelection(); selection.Toggle(first); selection.Toggle(second);
            Assert.That(selection.Pivot, Is.EqualTo(Vector3.right));
            selection.Toggle(child);
            Assert.That(selection.Contains(first), Is.False); Assert.That(selection.Count, Is.EqualTo(2));
            selection.Toggle(first);
            Assert.That(selection.Contains(child), Is.False); Assert.That(selection.Contains(first), Is.True);
            selection.Toggle(first); Assert.That(selection.Count, Is.EqualTo(1));
        }

        [Test] public void MoveUsesOriginalPoseInsteadOfAccumulatingEachFrame()
        {
            Transform target = Create("Target", Vector3.up);
            TransformSelection selection = new TransformSelection(); selection.Set(target);
            using TransformGroupEdit edit = new TransformGroupEdit(selection);
            edit.Move(Vector3.right); edit.Move(Vector3.right * 3f);
            Assert.That(target.position, Is.EqualTo(new Vector3(3f,1f,0f)));
            edit.Finish(false);
        }

        [Test] public void GroupRotationOrbitsAroundCenterAndRotatesEachObject()
        {
            Transform first = Create("First", Vector3.left);
            Transform second = Create("Second", Vector3.right);
            TransformSelection selection = new TransformSelection(); selection.Toggle(first); selection.Toggle(second);
            using TransformGroupEdit edit = new TransformGroupEdit(selection);
            edit.Rotate(Quaternion.AngleAxis(90f, Vector3.up));
            Assert.That(Vector3.Distance(first.position, Vector3.forward), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(second.position, Vector3.back), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(first.rotation, Quaternion.Euler(0f,90f,0f)), Is.LessThan(.001f));
            edit.Finish(false);
        }

        [Test] public void UniformGroupScaleChangesOffsetsAndPreservesParenting()
        {
            Transform parent = Create("Parent", new Vector3(3f,2f,1f));
            parent.rotation = Quaternion.Euler(0f,30f,0f); parent.localScale = new Vector3(2f,3f,4f);
            Transform first = Create("First", Vector3.left); first.SetParent(parent, true);
            Transform second = Create("Second", Vector3.right);
            Vector3 originalScale = first.localScale;
            TransformSelection selection = new TransformSelection(); selection.Toggle(first); selection.Toggle(second);
            using TransformGroupEdit edit = new TransformGroupEdit(selection);
            edit.Scale(Vector3.one * 2f, Quaternion.identity);
            Assert.That(Vector3.Distance(first.position, Vector3.left * 2f), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(second.position, Vector3.right * 2f), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(first.localScale, originalScale * 2f), Is.LessThan(.0001f));
            Assert.That(first.parent, Is.SameAs(parent)); edit.Finish(false);
        }

        [Test] public void CancellingRestoresPoseAndDynamicBodyState()
        {
            Transform target = Create("Physics target", Vector3.up);
            Rigidbody body = target.gameObject.AddComponent<Rigidbody>(); body.useGravity = false;
            body.linearVelocity = new Vector3(2f,3f,4f);
            TransformSelection selection = new TransformSelection(); selection.Set(target);
            using (TransformGroupEdit edit = new TransformGroupEdit(selection))
            {
                Assert.That(body.isKinematic, Is.True); edit.Move(Vector3.one * 10f); edit.Scale(Vector3.one * 4f, Quaternion.identity);
            }
            Assert.That(target.position, Is.EqualTo(Vector3.up)); Assert.That(target.localScale, Is.EqualTo(Vector3.one));
            Assert.That(body.isKinematic, Is.False); Assert.That(body.linearVelocity, Is.EqualTo(new Vector3(2f,3f,4f)));
        }

        [Test] public void SceneAddressRoundTripsDuplicateNamesAndReservedCharacters()
        {
            Transform first = Create("Address / : | %", Vector3.zero);
            Transform second = Create(first.name, Vector3.zero);
            Transform child = Create("Child / :", Vector3.zero); child.SetParent(second);
            Assert.That(SceneObjectAddress.TryCreate(first, out string firstId), Is.True);
            Assert.That(SceneObjectAddress.TryCreate(child, out string childId), Is.True);
            Assert.That(SceneObjectAddress.Resolve(firstId), Is.SameAs(first));
            Assert.That(SceneObjectAddress.Resolve(childId), Is.SameAs(child));
            Assert.That(SceneObjectAddress.Resolve("malformed"), Is.Null);
        }

        [Test] public void GizmoPicksAxisAndCenterAtDifferentCameraDistances()
        {
            Transform target = Create("Target", Vector3.zero);
            Camera camera = Create("Camera", Vector3.back * 5f).gameObject.AddComponent<Camera>();
            TransformSelection selection = new TransformSelection(); selection.Set(target);
            RuntimeTransformGizmo gizmo = Create("Gizmo", Vector3.zero).gameObject.AddComponent<RuntimeTransformGizmo>();
            gizmo.Initialize(Shader.Find("UnscriptedEngine/Runtime Transform Gizmo"));
            foreach (float distance in new[] { 5f, 20f })
            {
                camera.transform.position = Vector3.back * distance;
                gizmo.Present(camera, selection, TransformToolMode.Move, Vector3.zero, Quaternion.identity, TransformGizmoHandle.None);
                Assert.That(gizmo.Pick(camera, camera.WorldToScreenPoint(Vector3.zero)), Is.EqualTo(TransformGizmoHandle.Center));
                Assert.That(gizmo.Pick(camera, camera.WorldToScreenPoint(Vector3.right * gizmo.Size * .8f)), Is.EqualTo(TransformGizmoHandle.X));
                Assert.That(gizmo.Pick(camera, camera.WorldToScreenPoint(Vector3.up * gizmo.Size * .8f)), Is.EqualTo(TransformGizmoHandle.Y));
            }
        }

        [Test] public void InvalidPosesCannotPassTransportValidation()
        {
            Assert.That(new TransformPose(Vector3.zero, Quaternion.identity, Vector3.one).IsValid, Is.True);
            Assert.That(new TransformPose(new Vector3(float.NaN,0f,0f), Quaternion.identity, Vector3.one).IsValid, Is.False);
            Assert.That(new TransformPose(Vector3.zero, new Quaternion(0f,0f,0f,0f), Vector3.one).IsValid, Is.False);
            Assert.That(new TransformPose(Vector3.zero, Quaternion.identity, Vector3.zero).IsValid, Is.False);
        }

        [Test] public void CenterMoveDragFollowsCameraPlane()
        {
            Transform target = Create("Target", Vector3.zero);
            Camera camera = Create("Camera", Vector3.back * 5f).gameObject.AddComponent<Camera>();
            TransformSelection selection = new TransformSelection(); selection.Set(target);
            RuntimeTransformGizmo gizmo = Create("Gizmo", Vector3.zero).gameObject.AddComponent<RuntimeTransformGizmo>();
            gizmo.Initialize(Shader.Find("UnscriptedEngine/Runtime Transform Gizmo"));
            gizmo.Present(camera, selection, TransformToolMode.Move, Vector3.zero, Quaternion.identity, TransformGizmoHandle.None);
            using TransformGroupEdit edit = new TransformGroupEdit(selection);
            TransformHandleDrag drag = new TransformHandleDrag(edit, gizmo, TransformGizmoHandle.Center, camera, camera.WorldToScreenPoint(Vector3.zero));
            drag.Update(camera, camera.WorldToScreenPoint(new Vector3(1f,2f,0f)));
            Assert.That(Vector3.Distance(target.position, new Vector3(1f,2f,0f)), Is.LessThan(.001f));
        }

        [Test] public void FourthRotationRingPicksAndRotatesAroundCameraFacingAxis()
        {
            Transform first = Create("First", Vector3.left), second = Create("Second", Vector3.right);
            Camera camera = Create("Camera", Vector3.back * 5f).gameObject.AddComponent<Camera>();
            TransformSelection selection = new TransformSelection(); selection.Toggle(first); selection.Toggle(second);
            RuntimeTransformGizmo gizmo = Create("Gizmo", Vector3.zero).gameObject.AddComponent<RuntimeTransformGizmo>();
            gizmo.Initialize(Shader.Find("UnscriptedEngine/Runtime Transform Gizmo"));
            gizmo.Present(camera, selection, TransformToolMode.Rotate, Vector3.zero, Quaternion.identity, TransformGizmoHandle.None);
            Vector2 start = camera.WorldToScreenPoint(Vector3.right * gizmo.Size * 1.18f);
            Assert.That(gizmo.Pick(camera, start), Is.EqualTo(TransformGizmoHandle.Center));
            using TransformGroupEdit edit = new TransformGroupEdit(selection);
            TransformHandleDrag drag = new TransformHandleDrag(edit, gizmo, TransformGizmoHandle.Center, camera, start);
            drag.Update(camera, camera.WorldToScreenPoint(Vector3.up * gizmo.Size * 1.18f));
            Assert.That(Vector3.Distance(first.position, Vector3.down), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(second.position, Vector3.up), Is.LessThan(.001f));
        }

        [Test] public void AxisScaleDragChangesOnlyThatAxisForAnAlignedObject()
        {
            Transform target = Create("Target", Vector3.zero);
            Camera camera = Create("Camera", Vector3.back * 5f).gameObject.AddComponent<Camera>();
            TransformSelection selection = new TransformSelection(); selection.Set(target);
            RuntimeTransformGizmo gizmo = Create("Gizmo", Vector3.zero).gameObject.AddComponent<RuntimeTransformGizmo>();
            gizmo.Initialize(Shader.Find("UnscriptedEngine/Runtime Transform Gizmo"));
            gizmo.Present(camera, selection, TransformToolMode.Scale, Vector3.zero, Quaternion.identity, TransformGizmoHandle.None);
            using TransformGroupEdit edit = new TransformGroupEdit(selection);
            TransformHandleDrag drag = new TransformHandleDrag(edit, gizmo, TransformGizmoHandle.X, camera,
                camera.WorldToScreenPoint(Vector3.right * gizmo.Size));
            drag.Update(camera, camera.WorldToScreenPoint(Vector3.right * gizmo.Size * 2f));
            Assert.That(Vector3.Distance(target.localScale, new Vector3(2f,1f,1f)), Is.LessThan(.001f));
        }
    }
}
