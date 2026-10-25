using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class PhysicsContactSmoothingTests
    {
        private GameObject testObject;

        [TearDown]
        public void TearDown()
        {
            if (testObject != null)
                Object.DestroyImmediate(testObject);
        }

        [Test]
        public void ConsecutiveCorrectionsBeforeRenderPreserveAccumulatedOffset()
        {
            CharacterMovementComponent movement = CreateMovement();
            Call(movement, "ResetFixedPositionSamples", Vector3.zero);
            ApplyCorrection(movement, new Vector3(0.2f, 0f, 0f));
            AssertPosition((Vector3)Call(movement, "GetOwnerRenderPosition"), Vector3.zero);

            ApplyCorrection(movement, new Vector3(0.3f, 0f, 0f));
            AssertPosition((Vector3)Call(movement, "GetOwnerRenderPosition"), Vector3.zero);
            AssertPosition(Field<Vector3>(movement, "ownerCorrectionOffset"), new Vector3(-0.3f, 0f, 0f));
        }

        [Test]
        public void CorrectionPreservesFixedStepTrajectory()
        {
            CharacterMovementComponent movement = CreateMovement();
            Call(movement, "ResetFixedPositionSamples", Vector3.zero);
            SetField(movement, "previousFixedPosition", new Vector3(-0.1f, 0f, 0f));

            ApplyCorrection(movement, new Vector3(0.2f, 0f, 0f));

            AssertPosition(Field<Vector3>(movement, "previousFixedPosition"), new Vector3(0.1f, 0f, 0f));
            AssertPosition(Field<Vector3>(movement, "currentFixedPosition"), new Vector3(0.2f, 0f, 0f));
        }

        [Test]
        public void PhysicsRestoresCorrectedPositionAfterRenderSmoothing()
        {
            CharacterMovementComponent movement = CreateMovement();
            Call(movement, "ResetFixedPositionSamples", Vector3.zero);
            ApplyCorrection(movement, new Vector3(0.2f, 0f, 0f));
            Call(movement, "ApplyOwnerRenderInterpolation");
            Call(movement, "RestoreSimulationPosition");

            AssertPosition(movement.transform.position, new Vector3(0.2f, 0f, 0f));
            Assert.That(Field<bool>(movement, "hasInterpolatedTransform"), Is.False);
        }

        [Test]
        public void LargeCorrectionsSnapWithoutLeavingVisualOffset()
        {
            CharacterMovementComponent movement = CreateMovement();
            Call(movement, "ResetFixedPositionSamples", Vector3.zero);
            ApplyCorrection(movement, new Vector3(3f, 0f, 0f));

            AssertPosition(Field<Vector3>(movement, "ownerCorrectionOffset"), Vector3.zero);
            AssertPosition((Vector3)Call(movement, "GetOwnerRenderPosition"), new Vector3(3f, 0f, 0f));
        }

        [Test]
        public void DisabledCorrectionSmoothingLeavesNoOffset()
        {
            CharacterMovementComponent movement = CreateMovement();
            SetField(movement, "ownerCorrectionSmoothingSpeed", 0f);
            Call(movement, "ResetFixedPositionSamples", Vector3.zero);
            ApplyCorrection(movement, new Vector3(0.2f, 0f, 0f));

            AssertPosition(Field<Vector3>(movement, "ownerCorrectionOffset"), Vector3.zero);
        }

        [Test]
        public void VisualSmootherDisablesColliderRootInterpolation()
        {
            testObject = new GameObject("Physics prop");
            testObject.SetActive(false);
            testObject.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = testObject.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = true;

            GameObject model = new GameObject("Visual only");
            model.transform.SetParent(testObject.transform, false);
            NetworkPhysicsVisualSmoothing smoothing = testObject.AddComponent<NetworkPhysicsVisualSmoothing>();
            SetField(smoothing, "visualRoot", model.transform);
            Call(smoothing, "Awake");

            Assert.That(networkTransform.Interpolate, Is.False);
        }

        private CharacterMovementComponent CreateMovement()
        {
            testObject = new GameObject("Movement smoothing");
            return testObject.AddComponent<CharacterMovementComponent>();
        }

        private static void ApplyCorrection(CharacterMovementComponent movement, Vector3 correctedPosition)
        {
            Vector3 renderPosition = (Vector3)Call(movement, "GetOwnerRenderPosition");
            Vector3 simulationPosition = movement.SimulationPosition;
            Vector3 interpolationPosition = Field<Vector3>(movement, "previousFixedPosition");
            bool hasSamples = Field<bool>(movement, "hasFixedPositionSamples");

            movement.transform.position = correctedPosition;
            Call(movement, "RestoreOwnerInterpolationAfterCorrection", renderPosition,
                simulationPosition, interpolationPosition, hasSamples);
        }

        private static void AssertPosition(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00001f));
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static object Call(object target, string name, params object[] arguments)
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }
    }
}
