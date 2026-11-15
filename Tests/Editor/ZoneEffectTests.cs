using System.Collections.Generic;
using System.Reflection;
using Framework.GameplayTag;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Gameplay;

namespace UnscriptedEngine.Tests
{
    public sealed class ZoneEffectTests
    {
        private readonly List<Object> objects = new List<Object>();
        private static void Configure(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private GameObject CreateObject(string name)
        {
            GameObject created = new GameObject(name); objects.Add(created); return created;
        }
        private GameplayTag CreateTag(string path)
        {
            GameplayTag tag = ScriptableObject.CreateInstance<GameplayTag>();
            Configure(tag, "path", path); objects.Add(tag); return tag;
        }
        private TaggedFloatValue CreateHealth(GameObject actor, GameplayTag tag)
        {
            TaggedFloatValue health = actor.AddComponent<TaggedFloatValue>();
            health.Tags.Add(tag); health.TrySetValue(100f); return health;
        }
        [TearDown]
        public void Cleanup()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
                if (objects[index] != null) Object.DestroyImmediate(objects[index]);
            objects.Clear();
        }

        [Test]
        public void TaggedOperationAddsSetsAndIgnoresUnmatchedValues()
        {
            GameObject actor = CreateObject("Actor");
            GameplayTag healthTag = CreateTag("Stats.Health");
            TaggedFloatValue health = CreateHealth(actor, healthTag);
            TaggedFloatValue stamina = actor.AddComponent<TaggedFloatValue>();
            List<TaggedFloatValue> scratch = new List<TaggedFloatValue>();
            TaggedFloatOperation operation = new TaggedFloatOperation { ValueTag = healthTag, Amount = -15f };
            Assert.That(operation.Apply(actor, scratch), Is.True);
            Assert.That(health.Value, Is.EqualTo(85f));
            Assert.That(stamina.Value, Is.EqualTo(100f));
            operation.Operation = TaggedValueOperation.Set; operation.Amount = 20f;
            Assert.That(operation.Apply(actor, scratch), Is.True);
            Assert.That(health.Value, Is.EqualTo(20f));
            operation.ValueTag = null;
            Assert.That(operation.Apply(actor, scratch), Is.False);
        }

        [Test]
        public void DamageZoneAppliesMultipleChangesInOrderAndColorsTheirNetAdd()
        {
            GameplayTag healthTag = CreateTag("Stats.Health");
            GameplayTag staminaTag = CreateTag("Stats.Stamina");
            GameObject actor = CreateObject("Actor");
            TaggedFloatValue health = CreateHealth(actor, healthTag);
            TaggedFloatValue stamina = CreateHealth(actor, staminaTag);
            GameObject zoneObject = CreateObject("Damage Zone");
            zoneObject.AddComponent<BoxCollider>().isTrigger = true;
            ColliderZone zone = zoneObject.AddComponent<ColliderZone>();
            DamageZone damage = zoneObject.AddComponent<DamageZone>();
            damage.ValueChange.ValueTag = healthTag;
            damage.ValueChange.Amount = -20f;
            damage.AddValueChange(new TaggedFloatOperation { ValueTag = staminaTag, Amount = -5f });
            Assert.That(zone.DebugColor, Is.EqualTo(Color.red));
            Collider contact = actor.AddComponent<BoxCollider>();
            zoneObject.SendMessage("OnTriggerEnter", contact);
            Assert.That(health.Value, Is.EqualTo(80f));
            Assert.That(stamina.Value, Is.EqualTo(95f));
            damage.AddValueChange(new TaggedFloatOperation { ValueTag = healthTag, Amount = 30f });
            Assert.That(zone.DebugColor, Is.EqualTo(Color.green));
            damage.AddValueChange(new TaggedFloatOperation { ValueTag = healthTag, Amount = 50f, Operation = TaggedValueOperation.Set });
            Assert.That(zone.DebugColor, Is.EqualTo(Color.red), "Absolute Set cannot predict whether an actor heals.");
        }

        [Test]
        public void LegacyDamageChangeMigratesOnlyOnce()
        {
            GameObject zoneObject = CreateObject("Legacy Zone");
            zoneObject.AddComponent<BoxCollider>(); zoneObject.AddComponent<ColliderZone>();
            DamageZone damage = zoneObject.AddComponent<DamageZone>();
            TaggedFloatOperation legacy = new TaggedFloatOperation { Amount = -42f };
            Configure(damage, "valueChangesMigrated", false);
            Configure(damage, "valueChange", legacy);
            damage.OnAfterDeserialize();
            Assert.That(damage.ValueChanges.Count, Is.EqualTo(1));
            Assert.That(damage.ValueChange.Amount, Is.EqualTo(-42f));
            damage.AddValueChange(new TaggedFloatOperation { Amount = 10f });
            // Unity can reconstruct null inline classes; the migration marker must prevent overwriting the list.
            Configure(damage, "valueChange", new TaggedFloatOperation());
            damage.OnAfterDeserialize();
            Assert.That(damage.ValueChanges.Count, Is.EqualTo(2));
            Assert.That(damage.ValueChange.Amount, Is.EqualTo(-42f));
        }

        [Test]
        public void StatusZoneUsesDefinitionColorAndFallsBackWhenDisabled()
        {
            GameObject zoneObject = CreateObject("Status Zone");
            zoneObject.AddComponent<BoxCollider>().isTrigger = true;
            ColliderZone zone = zoneObject.AddComponent<ColliderZone>();
            StatusZone status = zoneObject.AddComponent<StatusZone>();
            PoisonEffect poison = ScriptableObject.CreateInstance<PoisonEffect>(); objects.Add(poison);
            poison.DebugColor = Color.magenta;
            status.Effect = poison;
            Assert.That(zone.DebugColor, Is.EqualTo(Color.magenta));
            status.enabled = false;
            Assert.That(zone.DebugColor, Is.EqualTo(new Color(0.15f, 0.55f, 1f)));
        }

        [Test]
        public void DamageIsOncePerActorVisitAcrossMultipleCollidersAndRepeatsOnSchedule()
        {
            GameplayTag tag = CreateTag("Stats.Health");
            GameObject actor = CreateObject("Actor");
            actor.AddComponent<GameplayTags>();
            TaggedFloatValue health = CreateHealth(actor, tag);
            GameObject child = CreateObject("Child Collider"); child.transform.SetParent(actor.transform);
            Collider first = actor.AddComponent<BoxCollider>();
            Collider second = child.AddComponent<SphereCollider>();
            GameObject zoneObject = CreateObject("Damage Zone");
            zoneObject.AddComponent<BoxCollider>().isTrigger = true;
            ColliderZone zone = zoneObject.AddComponent<ColliderZone>();
            DamageZone damage = zoneObject.AddComponent<DamageZone>();
            damage.ValueChange.ValueTag = tag;
            zoneObject.SendMessage("OnTriggerEnter", first);
            zoneObject.SendMessage("OnTriggerEnter", second);
            damage.SendMessage("Update"); damage.SendMessage("Update");
            Assert.That(health.Value, Is.EqualTo(90f));
            zoneObject.SendMessage("OnTriggerExit", first);
            damage.SendMessage("Update");
            Assert.That(health.Value, Is.EqualTo(90f));
            zoneObject.SendMessage("OnTriggerExit", second);
            damage.SendMessage("Update");
            zoneObject.SendMessage("OnTriggerEnter", first);
            damage.SendMessage("Update");
            Assert.That(health.Value, Is.EqualTo(80f));
            damage.Ongoing = true;
            // A contact tick is not allowed before the interval, even with many render frames.
            damage.SendMessage("Update");
            Assert.That(health.Value, Is.EqualTo(80f));
            damage.enabled = false; damage.enabled = true;
            damage.SendMessage("Update");
            Assert.That(health.Value, Is.EqualTo(70f));
        }

        [Test]
        public void PoisonTicksImmediatelyAndRefreshesWithoutStackingOrExtraDamage()
        {
            GameObject actor = CreateObject("Poisoned Actor");
            GameplayTag tag = CreateTag("Stats.Health");
            TaggedFloatValue health = CreateHealth(actor, tag);
            PoisonEffect poison = ScriptableObject.CreateInstance<PoisonEffect>(); objects.Add(poison);
            poison.ValueChange.ValueTag = tag;
            StatusEffectController controller = actor.AddComponent<StatusEffectController>();
            Assert.That(controller.Apply(poison), Is.True);
            Assert.That(health.Value, Is.EqualTo(90f));
            Assert.That(controller.Apply(poison), Is.True);
            Assert.That(controller.ActiveCount, Is.EqualTo(1));
            Assert.That(health.Value, Is.EqualTo(90f));
            controller.SendMessage("Update");
            Assert.That(health.Value, Is.EqualTo(90f));
            Assert.That(controller.Remove(poison), Is.True);
            Assert.That(controller.ActiveCount, Is.Zero);
            controller.Apply(poison);
            controller.enabled = false;
            Assert.That(controller.ActiveCount, Is.Zero);
        }

        [Test]
        public void CreatorAddsConfiguredEffectBesideColliderZone()
        {
            UnscriptedEngine.Editor.ColliderZoneCreatorWindow window = ScriptableObject.CreateInstance<UnscriptedEngine.Editor.ColliderZoneCreatorWindow>();
            Object selection = Selection.activeObject;
            GameObject created = null;
            try
            {
                Configure(window, "placeAtViewCenter", false);
                System.Type kind = typeof(UnscriptedEngine.Editor.ColliderZoneCreatorWindow).GetNestedType("ZoneKind", BindingFlags.NonPublic);
                Configure(window, "zoneKind", System.Enum.Parse(kind, "Damage"));
                Selection.activeObject = null;
                typeof(UnscriptedEngine.Editor.ColliderZoneCreatorWindow).GetMethod("CreateZone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                created = Selection.activeGameObject;
                Assert.That(created.GetComponent<ColliderZone>(), Is.Not.Null);
                Assert.That(created.GetComponent<DamageZone>(), Is.Not.Null);
                Assert.That(created.GetComponent<StatusZone>(), Is.Null);
            }
            finally
            {
                if (created != null) Object.DestroyImmediate(created);
                Object.DestroyImmediate(window); Selection.activeObject = selection;
            }
        }
    }
}
