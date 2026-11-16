using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class MovingGroundInteractionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private GameObject Create(string name)
        {
            GameObject result = new GameObject(name);
            objects.Add(result);
            return result;
        }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        [TearDown] public void Cleanup()
        {
            foreach (GameObject value in objects) if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
        }
        [Test] public void LargeObjectCanBeUsedFromItsSurface()
        {
            GameObject target = Create("Scaled interactable");
            target.transform.position = Vector3.right * 20f;
            target.transform.localScale = Vector3.one * 40f;
            target.AddComponent<BoxCollider>();
            UInteractableComponent interactable = target.AddComponent<UInteractableComponent>();
            GameObject source = Create("Interactor");
            source.transform.position = Vector3.left;
            Physics.SyncTransforms();
            Assert.IsTrue(interactable.CanOfferInteraction(source));
            Assert.IsTrue(interactable.TryBeginInteraction(source));
            source.transform.position = Vector3.left * 4f;
            Assert.IsFalse(interactable.CanOfferInteraction(source));
            Assert.IsFalse(interactable.TryBeginInteraction(source));
        }
        [Test] public void ExplicitInteractionPointKeepsItsRangeRule()
        {
            GameObject target = Create("Large object");
            target.transform.position = Vector3.right * 20f;
            target.AddComponent<BoxCollider>().size = Vector3.one * 40f;
            UInteractableComponent interactable = target.AddComponent<UInteractableComponent>();
            Set(interactable, "interactionPoint", target.transform);
            Physics.SyncTransforms();
            Assert.IsFalse(interactable.CanOfferInteraction(Create("Source")));
        }
        [Test] public void CompletedSingleUseTargetIsNotOffered()
        {
            UInteractableComponent target = Create("Single use").AddComponent<UInteractableComponent>();
            Set(target, "repeatable", false);
            GameObject source = Create("Source");
            Assert.IsTrue(target.TryBeginInteraction(source));
            Assert.IsFalse(target.CanOfferInteraction(source));
        }
        [Test] public void GroundCarryFollowsTranslationAndRotationWithoutPlatformComponent()
        {
            CharacterMovementComponent movement = Create("Rider").AddComponent<CharacterMovementComponent>();
            Transform support = Create("Any moving object").transform;
            Set(movement, "groundSupport", support);
            Set(movement, "supportLocalPoint", Vector3.right * 2f);
            Set(movement, "supportWorldPoint", Vector3.right * 2f);
            support.position = Vector3.up;
            support.rotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 displacement = (Vector3)typeof(CharacterMovementComponent).GetMethod("GetSupportDisplacement",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null);
            Assert.That(Vector3.Distance(displacement, support.TransformPoint(Vector3.right * 2f) - Vector3.right * 2f), Is.LessThan(0.0001f));
            support.position = Vector3.up * 100f;
            Assert.AreEqual(Vector3.zero, typeof(CharacterMovementComponent).GetMethod("GetSupportDisplacement",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null));
            Object.DestroyImmediate(support.gameObject);
            Assert.AreEqual(Vector3.zero, typeof(CharacterMovementComponent).GetMethod("GetSupportDisplacement",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null));
        }
    }
}
