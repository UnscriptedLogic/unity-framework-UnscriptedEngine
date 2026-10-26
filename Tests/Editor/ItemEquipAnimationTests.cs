using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class ItemEquipAnimationTests
    {
        private GameObject anchor;
        private GameObject visual;

        [SetUp]
        public void SetUp()
        {
            anchor = new GameObject("Equip anchor");
            visual = new GameObject("Item visual");
            anchor.transform.localPosition = new Vector3(1f, 2f, 3f);
            anchor.transform.localRotation = Quaternion.Euler(5f, 20f, 10f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(anchor);
            Object.DestroyImmediate(visual);
        }

        [Test]
        public void ReplayAndDisableRestoreAuthoredAnchorPose()
        {
            var animation = anchor.AddComponent<ItemEquipAnimation>();
            Vector3 position = anchor.transform.localPosition;
            Quaternion rotation = anchor.transform.localRotation;
            for (int i = 0; i < 3; i++)
            {
                animation.Play(visual);
                Tick(animation);
                Assert.That(Vector3.Distance(position, anchor.transform.localPosition), Is.GreaterThan(0.1f));
            }
            animation.enabled = false;
            Assert.That(Vector3.Distance(position, anchor.transform.localPosition), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(rotation, anchor.transform.localRotation), Is.LessThan(0.001f));
            Assert.That(animation.IsPlaying, Is.False);
        }

        [Test]
        public void EnabledChildOverrideReplacesDefaultsAndZeroDurationSkipsMotion()
        {
            var animation = anchor.AddComponent<ItemEquipAnimation>();
            var child = new GameObject("Override");
            child.transform.SetParent(visual.transform);
            var custom = child.AddComponent<ItemEquipAnimationOverride>();
            Vector3 position = anchor.transform.localPosition;
            custom.Evaluate(0f, out Vector3 offset, out Quaternion rotation);
            animation.Play(visual);
            Tick(animation);
            Assert.That(Vector3.Distance(anchor.transform.localPosition, position + offset), Is.LessThan(0.00001f));
            animation.Stop();
            typeof(ItemEquipAnimationOverride).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(custom, 0f);
            animation.Play(visual);
            Assert.That(animation.IsPlaying, Is.False);
            Tick(animation);
            Assert.That(Vector3.Distance(anchor.transform.localPosition, position), Is.LessThan(0.00001f));
        }

        [Test]
        public void UnequipNotificationCancelsAnimation()
        {
            var equipment = anchor.AddComponent<ItemEquipComponent>();
            var animation = anchor.AddComponent<ItemEquipAnimation>();
            Vector3 position = anchor.transform.localPosition;
            animation.Play(visual);
            Tick(animation);
            // Clearing the held state raises the same event used by the animation listener.
            typeof(ItemEquipComponent).GetField("heldVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(equipment, visual);
            typeof(ItemEquipComponent).GetMethod("ClearHeldVisual", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(equipment, null);
            Assert.That(animation.IsPlaying, Is.False);
            Assert.That(Vector3.Distance(anchor.transform.localPosition, position), Is.LessThan(0.00001f));
        }

        private static void Tick(ItemEquipAnimation animation) =>
            typeof(ItemEquipAnimation).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(animation, null);
    }
}
