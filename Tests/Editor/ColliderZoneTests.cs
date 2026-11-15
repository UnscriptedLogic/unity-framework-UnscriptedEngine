using System.Collections.Generic;
using System.Reflection;
using Framework.GameplayTag;
using NUnit.Framework;
using UnityEngine;
using UnscriptedEngine.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine.Tests
{
    public sealed class ColliderZoneTests
    {
        private readonly List<Object> objects = new List<Object>();

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private GameplayTag Tag(string path, GameplayTag parent = null)
        {
            GameplayTag tag = ScriptableObject.CreateInstance<GameplayTag>();
            Set(tag, "path", path); Set(tag, "parent", parent); objects.Add(tag); return tag;
        }

        private GameObject CreateObject(string name)
        {
            GameObject created = new GameObject(name); objects.Add(created); return created;
        }

        [TearDown]
        public void Cleanup()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
                if (objects[index] != null) Object.DestroyImmediate(objects[index]);
            objects.Clear();
        }

        [Test]
        public void EmptyFilterAllowsUntaggedColliderAndEveryLayer()
        {
            Collider collider = CreateObject("Untagged").AddComponent<BoxCollider>();
            ColliderZoneFilter filter = new ColliderZoneFilter();
            for (int layer = 0; layer < 32; layer++)
            {
                collider.gameObject.layer = layer;
                Assert.That(filter.Allows(collider), Is.True, "Layer " + layer);
            }
            Assert.That(filter.Allows(null), Is.False);
        }

        [TestCase("requireAny", true, true)]
        [TestCase("requireAll", false, true)]
        [TestCase("rejectAny", false, false)]
        [TestCase("rejectAll", true, false)]
        public void AnyAndAllConditionsMatchOneThenBothTags(string field, bool oneMatch, bool bothMatch)
        {
            GameplayTag player = Tag("Actor.Player");
            GameplayTag key = Tag("Inventory.Key");
            ColliderZoneFilter filter = new ColliderZoneFilter();
            Set(filter, field, new[] { player, key });
            GameplayTagContainer tags = new GameplayTagContainer();
            tags.Add(player);
            Assert.That(filter.MatchesTags(tags), Is.EqualTo(oneMatch));
            tags.Add(key);
            Assert.That(filter.MatchesTags(tags), Is.EqualTo(bothMatch));
        }

        [Test]
        public void ChildColliderUsesNearestParentOwnerAndItsOwnLayer()
        {
            GameplayTag actor = Tag("Actor");
            GameplayTag player = Tag("Actor.Player", actor);
            GameObject parent = CreateObject("Player");
            parent.AddComponent<GameplayTags>().Tags.Add(player);
            GameObject child = CreateObject("Contact");
            child.transform.SetParent(parent.transform);
            child.layer = 7;
            Collider collider = child.AddComponent<BoxCollider>();
            ColliderZoneFilter filter = new ColliderZoneFilter();
            Set(filter, "requireAll", new[] { actor });
            Assert.That(filter.Allows(collider), Is.True);
            filter.Layers = 1 << 6;
            Assert.That(filter.Allows(collider), Is.False);
            filter.Layers = 1 << 7;
            Assert.That(filter.Allows(collider), Is.True);
            child.AddComponent<GameplayTags>();
            Assert.That(filter.Allows(collider), Is.False);
        }

        [Test]
        public void RejectionOverridesRequiredTagsAndMissingOwnerFailsRequirements()
        {
            GameplayTag player = Tag("Actor.Player");
            ColliderZoneFilter filter = new ColliderZoneFilter();
            Set(filter, "requireAny", new[] { player });
            Assert.That(filter.MatchesTags(null), Is.False);
            GameplayTagContainer tags = new GameplayTagContainer(); tags.Add(player);
            Set(filter, "rejectAll", new[] { player });
            Assert.That(filter.MatchesTags(tags), Is.False);
        }

        [Test]
        public void BoxOutlinePreservesRotationCenterAndScale()
        {
            GameObject zone = CreateObject("Zone");
            zone.transform.SetPositionAndRotation(new Vector3(3f, 4f, 5f), Quaternion.Euler(10f, 35f, 20f));
            zone.transform.localScale = new Vector3(2f, 3f, 4f);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.center = Vector3.one; box.size = new Vector3(2f, 4f, 6f);
            List<Vector3> lines = new List<Vector3>();
            ColliderZoneWireframe.Collect(box, lines);
            Assert.That(lines.Count, Is.EqualTo(24));
            foreach (Vector3 point in lines)
            {
                Vector3 local = zone.transform.InverseTransformPoint(point) - box.center;
                Assert.That(Mathf.Abs(local.x), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(Mathf.Abs(local.y), Is.EqualTo(2f).Within(0.0001f));
                Assert.That(Mathf.Abs(local.z), Is.EqualTo(3f).Within(0.0001f));
            }
        }

        [Test]
        public void CreatorUsesSelectedParentCopiesFiltersAndSupportsUndo()
        {
            Scene previous = SceneManager.GetActiveScene();
            Object selection = Selection.activeObject;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            UnscriptedEngine.Editor.ColliderZoneCreatorWindow window = ScriptableObject.CreateInstance<UnscriptedEngine.Editor.ColliderZoneCreatorWindow>();
            try
            {
                Set(window, "placeAtViewCenter", false);
                GameObject parent = new GameObject("Zone Parent");
                Selection.activeGameObject = parent;
                ColliderZone template = (ColliderZone)typeof(UnscriptedEngine.Editor.ColliderZoneCreatorWindow)
                    .GetField("template", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                template.Filter.Layers = 1 << 9;
                typeof(UnscriptedEngine.Editor.ColliderZoneCreatorWindow).GetMethod("CreateZone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                ColliderZone created = Selection.activeGameObject.GetComponent<ColliderZone>();
                Assert.That(created.transform.parent, Is.EqualTo(parent.transform));
                Assert.That(created.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(created.Shape.isTrigger, Is.True);
                Assert.That(created.Filter.Layers.value, Is.EqualTo(1 << 9));
                Assert.That(created.GetComponent<Rigidbody>().isKinematic, Is.True);
                Undo.PerformUndo();
                Assert.That(parent.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(window);
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previous);
                Selection.activeObject = selection;
            }
        }

        [Test]
        public void DebugShaderImportsWithoutErrors()
        {
            Shader shader = Resources.Load<Shader>("ColliderZoneDebug");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void ControllerContactEntersOnceStaysAndExitsAfterMovingAway()
        {
            GameObject zoneObject = CreateObject("Solid Zone");
            BoxCollider box = zoneObject.AddComponent<BoxCollider>();
            ColliderZone zone = zoneObject.AddComponent<ColliderZone>();
            CharacterController controller = CreateObject("Controller").AddComponent<CharacterController>();
            controller.transform.position = new Vector3(0.95f, 0f, 0f);
            int entries = 0;
            int stays = 0;
            int exits = 0;
            zone.Entered.AddListener(other => { Assert.That(other, Is.SameAs(controller)); entries++; });
            zone.Stayed.AddListener(other => stays++);
            zone.Exited.AddListener(other => exits++);
            Vector3 point = new Vector3(0.5f, 0f, 0f);
            zone.ReportControllerContact(controller, point);
            zone.ReportControllerContact(controller, point);
            Assert.That(entries, Is.EqualTo(1));
            Assert.That(stays, Is.EqualTo(1));
            // Exercise physics-step cleanup while no further movement hits arrive.
            zone.SendMessage("FixedUpdate");
            Assert.That(zone.ContactCount, Is.EqualTo(1), "Idle contact must survive controller skin separation.");
            controller.transform.position = Vector3.right * 10f;
            zone.SendMessage("FixedUpdate");
            Assert.That(zone.ContactCount, Is.Zero);
            Assert.That(exits, Is.EqualTo(1));
        }

        [Test]
        public void ControllerForwardingHonorsLayerFilterAndIgnoresTriggerZones()
        {
            GameObject zoneObject = CreateObject("Zone");
            BoxCollider box = zoneObject.AddComponent<BoxCollider>();
            ColliderZone zone = zoneObject.AddComponent<ColliderZone>();
            CharacterController controller = CreateObject("Controller").AddComponent<CharacterController>();
            zone.Filter.Layers = 0;
            zone.ReportControllerContact(controller, Vector3.zero);
            Assert.That(zone.ContactCount, Is.Zero);
            zone.Filter.Layers = ~0;
            box.isTrigger = true;
            zone.ReportControllerContact(controller, Vector3.zero);
            Assert.That(zone.ContactCount, Is.Zero);
            box.isTrigger = false;
            zone.ReportControllerContact(controller, Vector3.zero);
            Assert.That(zone.ContactCount, Is.EqualTo(1));
            zone.enabled = false;
            Assert.That(zone.ContactCount, Is.Zero);
        }
    }
}
