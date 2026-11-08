using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class TaggedFloatValueTests
    {
        private GameObject owner;
        private TaggedFloatValue value;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Tagged float test");
            value = owner.AddComponent<TaggedFloatValue>();
            value.SendMessage("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void OfflineChangesClampAndOnlyNotifyWhenChanged()
        {
            int notifications = 0;
            float received = -1f;
            value.OnValueChanged.AddListener(next => { notifications++; received = next; });
            Assert.That(value.TrySetValue(150f), Is.True);
            Assert.That(value.Value, Is.EqualTo(100f));
            value.AddValue(-125f);
            Assert.That(value.Value, Is.Zero);
            Assert.That(received, Is.Zero);
            value.SetValue(-10f);
            Assert.That(notifications, Is.EqualTo(1));
            value.ResetValue();
            Assert.That(value.Value, Is.EqualTo(100f));
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void CustomBoundsAndDisabledClampAreRespected()
        {
            SerializedObject settings = new SerializedObject(value);
            settings.FindProperty("minimum").floatValue = -20f;
            settings.FindProperty("maximum").floatValue = 40f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            value.SetValue(-100f);
            Assert.That(value.Value, Is.EqualTo(-20f));
            value.SetValue(100f);
            Assert.That(value.Value, Is.EqualTo(40f));
            settings.Update();
            settings.FindProperty("clampValue").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            value.SetValue(250f);
            Assert.That(value.Value, Is.EqualTo(250f));
        }

        [Test]
        public void NonFiniteInputsDoNotCorruptValueOrNotifyListeners()
        {
            value.SetValue(25f);
            int notifications = 0;
            value.OnValueChanged.AddListener(next => notifications++);
            Assert.That(value.TrySetValue(float.NaN), Is.False);
            Assert.That(value.TrySetValue(float.PositiveInfinity), Is.False);
            Assert.That(value.TrySetValue(float.NegativeInfinity), Is.False);
            Assert.That(value.Value, Is.EqualTo(25f));
            Assert.That(notifications, Is.Zero);
        }
    }
}
