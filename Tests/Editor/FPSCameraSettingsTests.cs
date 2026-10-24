using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class FPSCameraSettingsTests
    {
        [Test]
        public void DisablingDynamicFovImmediatelyRestoresOriginalLens()
        {
            GameObject cameraObject = new GameObject("FOV Settings Test", typeof(Camera));
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.fieldOfView = 60f;
                FPSCameraTweaks tweaks = cameraObject.AddComponent<FPSCameraTweaks>();
                SerializedObject serialized = new SerializedObject(tweaks);
                serialized.FindProperty("CameraRoot").objectReferenceValue = cameraObject.transform;
                serialized.FindProperty("targetCamera").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                MethodInfo updateFov = typeof(FPSCameraTweaks).GetMethod(
                    "UpdateDynamicFov", BindingFlags.Instance | BindingFlags.NonPublic);
                updateFov.Invoke(tweaks, new object[] { 1f, 10f });
                Assert.Greater(camera.fieldOfView, 60f);
                tweaks.SetDynamicFovEnabled(false);
                Assert.AreEqual(60f, camera.fieldOfView);
                Assert.IsTrue(tweaks.enabled, "Disabling FOV must not disable other camera effects.");
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }
    }
}
