using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UnscriptedEngine.Settings;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class DebugEntityTests
    {
        private GameObject root;
        private GameObject blocker;
        private DebugEntity entity;
        private LayerMask previousLayers;
        private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            previousLayers = DebugEntities.PhysicsLayers;
            DebugEntities.PhysicsLayers = 1 << 31;
            root = new GameObject("Debug test entity");
            root.transform.position = Origin + Vector3.forward * 5f;
            entity = root.AddComponent<DebugEntity>();
            DebugEntities.ClearPin();
            DebugEntities.SetLookedAt(null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugEntities.ClearPin();
            DebugEntities.SetLookedAt(null);
            DebugEntities.PhysicsLayers = previousLayers;
            Object.DestroyImmediate(root);
            if (blocker != null) Object.DestroyImmediate(blocker);
            yield return new ExitPlayMode();
        }

        [Test]
        public void RegistryAndPinFollowEntityLifetime()
        {
            Assert.IsTrue(DebugEntities.Registered.Contains(entity));
            DebugEntities.SetLookedAt(entity);
            DebugEntities.TogglePin();
            DebugEntities.SetLookedAt(null);
            Assert.AreSame(entity, DebugEntities.Inspected);
            entity.enabled = false;
            Assert.IsNull(DebugEntities.Pinned);
            Assert.IsFalse(DebugEntities.Registered.Contains(entity));
            entity.enabled = true;
            Assert.IsTrue(DebugEntities.Registered.Contains(entity));
            Assert.IsNull(DebugEntities.Pinned);
        }

        [Test]
        public void ChildColliderFindsParentAndSolidObstacleBlocksInspection()
        {
            GameObject child = new GameObject("Child collider", typeof(BoxCollider));
            child.layer = 31;
            child.transform.SetParent(root.transform, false);
            Physics.SyncTransforms();
            Ray ray = new Ray(Origin, Vector3.forward);
            Assert.AreSame(entity, DebugEntities.FindTarget(ray, 10f));
            Assert.IsNull(DebugEntities.FindTarget(ray, 2f));

            blocker = new GameObject("Wall", typeof(BoxCollider));
            blocker.layer = 31;
            blocker.transform.position = Origin + Vector3.forward * 2f;
            Physics.SyncTransforms();
            Assert.IsNull(DebugEntities.FindTarget(ray, 10f));
            blocker.GetComponent<BoxCollider>().isTrigger = true;
            Physics.SyncTransforms();
            Assert.AreSame(entity, DebugEntities.FindTarget(ray, 10f));
        }

        [Test]
        public void ProvidersCollectOnlyOnDemandAndSkipDisabledSources()
        {
            DebugInfoTestSource source = root.AddComponent<DebugInfoTestSource>();
            Assert.AreEqual(0, source.Collections);
            DebugInfoWriter writer = new DebugInfoWriter();
            string details = entity.Capture(writer);
            StringAssert.Contains("Entity: Debug test entity", details);
            StringAssert.Contains("Health: 42", details);
            Assert.AreEqual(1, source.Collections);
            source.enabled = false;
            StringAssert.DoesNotContain("Health:", entity.Capture(writer));
            Assert.AreEqual(1, source.Collections);
        }

        [Test]
        public void ViewPermissionsAndLocalLabelOffsetAreRespected()
        {
            entity.Views = DebugEntityViews.Hud;
            DebugEntities.SetLookedAt(entity);
            Assert.IsNull(DebugEntities.LookedAt);
            Assert.IsTrue(entity.Supports(DebugEntityViews.Hud));
            Assert.IsFalse(entity.Supports(DebugEntityViews.World));
            entity.LabelOffset = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(root.transform.TransformPoint(entity.LabelOffset), entity.LabelPosition);
        }

        [Test]
        public void WriterBoundsTextAndPreventsInjectedLines()
        {
            DebugInfoWriter writer = new DebugInfoWriter();
            writer.Add("State", "Idle\nInjected");
            Assert.AreEqual("State: Idle Injected", writer.ToString());
            writer.Clear();
            for (int index = 0; index < 100; index++) writer.Add("Value", index);
            Assert.AreEqual(24, writer.ToString().Split('\n').Length);
            writer.Clear();
            writer.Add(new string('x', 5000), new string('y', 5000));
            Assert.LessOrEqual(writer.ToString().Length, 4096);
        }

        [Test]
        public void OcclusionIgnoresOwnColliderButDetectsWall()
        {
            blocker = new GameObject("Debug camera", typeof(Camera));
            blocker.transform.position = Origin;
            Camera camera = blocker.GetComponent<Camera>();
            entity.LabelOffset = Vector3.zero;
            root.layer = 31;
            root.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Assert.IsFalse(EntityDebugDisplay.IsOccluded(camera, entity));
            GameObject wall = new GameObject("Wall", typeof(BoxCollider));
            wall.layer = 31;
            wall.transform.SetParent(blocker.transform, false);
            wall.transform.localPosition = Vector3.forward * 2f;
            Physics.SyncTransforms();
            Assert.IsTrue(EntityDebugDisplay.IsOccluded(camera, entity));
        }

        [Test]
        public void AllThreeViewsShareOneSnapshotAndHideTogether()
        {
            SettingsService settings = SettingsRuntime.Service;
            using (System.IDisposable registration = settings.Register(DebugEntities.Definitions))
            {
                bool oldInspect = settings.Get(DebugEntities.InspectSetting);
                bool oldHud = settings.Get(DebugEntities.HudSetting);
                bool oldWorld = settings.Get(DebugEntities.WorldSetting);
                bool oldOcclusion = settings.Get(DebugEntities.OcclusionSetting);
                float oldDistance = settings.Get(DebugEntities.DistanceSetting);
                Camera oldCamera = DebugEntities.CameraOverride;
                try
                {
                    settings.TrySet(DebugEntities.InspectSetting, true);
                    settings.TrySet(DebugEntities.HudSetting, true);
                    settings.TrySet(DebugEntities.WorldSetting, true);
                    settings.TrySet(DebugEntities.OcclusionSetting, false);
                    settings.TrySet(DebugEntities.DistanceSetting, 10f);
                    blocker = new GameObject("View host", typeof(Camera));
                    blocker.transform.position = Origin;
                    DebugEntities.CameraOverride = blocker.GetComponent<Camera>();
                    entity.LabelOffset = Vector3.zero;
                    root.layer = 31;
                    root.AddComponent<BoxCollider>();
                    DebugInfoTestSource source = root.AddComponent<DebugInfoTestSource>();
                    GameObject screen = new GameObject("Screen", typeof(RectTransform), typeof(Canvas));
                    screen.transform.SetParent(blocker.transform, false);
                    screen.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    EntityDebugDisplay display = new EntityDebugDisplay(blocker, screen,
                        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                    Physics.SyncTransforms();
                    System.Text.StringBuilder overlay = new System.Text.StringBuilder();
                    display.Refresh(overlay);
                    StringAssert.Contains("Looked-at entity", overlay.ToString());
                    StringAssert.Contains("Health: 42", overlay.ToString());
                    Assert.AreEqual(1, source.Collections);
                    Text[] labels = blocker.GetComponentsInChildren<Text>(true);
                    Assert.AreEqual(2, labels.Length);
                    foreach (Text label in labels)
                    {
                        Assert.IsTrue(label.gameObject.activeInHierarchy);
                        StringAssert.Contains("Health: 42", label.text);
                    }
                    display.Hide();
                    display.UpdatePositions();
                    foreach (Text label in labels) Assert.IsFalse(label.gameObject.activeInHierarchy);
                }
                finally
                {
                    DebugEntities.CameraOverride = oldCamera;
                    settings.TrySet(DebugEntities.InspectSetting, oldInspect);
                    settings.TrySet(DebugEntities.HudSetting, oldHud);
                    settings.TrySet(DebugEntities.WorldSetting, oldWorld);
                    settings.TrySet(DebugEntities.OcclusionSetting, oldOcclusion);
                    settings.TrySet(DebugEntities.DistanceSetting, oldDistance);
                }
            }
        }
    }
}
