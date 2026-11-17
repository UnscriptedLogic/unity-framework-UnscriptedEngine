using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnscriptedEngine.Gameplay;

namespace UnscriptedEngine.Tests
{
    public sealed class PhysicsForceZoneTests
    {
        private Scene scene;
        private PhysicsScene physics;
        private GameObject zoneObject;
        private ColliderZone zone;
        private Rigidbody body;
        private Collider contact;

        [SetUp]
        public void Setup()
        {
            scene = SceneManager.CreateScene("Force Zone Test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
            zoneObject = CreateObject("Zone", Vector3.zero);
            zoneObject.AddComponent<BoxCollider>().isTrigger = true;
            zone = zoneObject.AddComponent<ColliderZone>();
            GameObject target = CreateObject("Body", Vector3.up * 3f);
            body = target.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = 0f;
            contact = target.AddComponent<BoxCollider>();
        }

        private GameObject CreateObject(string name, Vector3 position)
        {
            GameObject created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            created.transform.position = position;
            return created;
        }

        [TearDown]
        public void Cleanup() => EditorSceneManager.CloseScene(scene, true);

        private void Enter() => zoneObject.SendMessage("OnTriggerEnter", contact);
        private void Step()
        {
            zoneObject.SendMessage("FixedUpdate");
            physics.Simulate(0.02f);
        }

        private static object CallMovement(CharacterMovementComponent movement, string method, params object[] arguments) =>
            typeof(CharacterMovementComponent).GetMethod(method, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(movement, arguments);

        [TestCase("wind", 1f)]
        [TestCase("suction", -1f)]
        [TestCase("knockback", 1f)]
        public void ZonesAffectControllerMovementWithoutRigidbody(string kind, float sign)
        {
            GameObject player = CreateObject("Player", Vector3.up * 3f);
            CharacterMovementComponent movement = player.AddComponent<CharacterMovementComponent>();
            CallMovement(movement, "Awake");
            Collider controller = player.GetComponent<CharacterController>();
            PhysicsForceZone effect;
            if (kind == "wind") effect = zoneObject.AddComponent<UnscriptedEngine.Gameplay.WindZone>();
            else if (kind == "suction") effect = zoneObject.AddComponent<SuctionZone>();
            else effect = zoneObject.AddComponent<KnockbackZone>();
            effect.Strength = 20f;
            effect.RequireLineOfSight = false;
            zoneObject.SendMessage("OnTriggerEnter", controller);
            zoneObject.SendMessage("FixedUpdate");
            CharacterMovementComponent.CharacterMoveInput input =
                (CharacterMovementComponent.CharacterMoveInput)CallMovement(movement, "CreateMoveInput");
            float expected = kind == "knockback" ? 20f : 20f * Time.fixedDeltaTime;
            Assert.That(input.ExternalVelocityChange.y, Is.EqualTo(expected * sign).Within(0.001f));
            CharacterMovementComponent.CharacterMoveInput next =
                (CharacterMovementComponent.CharacterMoveInput)CallMovement(movement, "CreateMoveInput");
            Assert.That(next.ExternalVelocityChange, Is.EqualTo(Vector3.zero), "Each queued force is consumed exactly once.");
            input.IsFlying = true;
            CallMovement(movement, "SimulateMove", input);
            Assert.That(movement.Velocity.y * sign, Is.GreaterThan(0f), "Movement simulation must retain the force.");
        }

        [Test]
        public void ControllerCompoundCollidersReceiveOneKnockbackPerVisit()
        {
            GameObject player = CreateObject("Player", Vector3.up * 3f);
            CharacterMovementComponent movement = player.AddComponent<CharacterMovementComponent>();
            Collider controller = player.GetComponent<CharacterController>();
            GameObject child = CreateObject("Player child", player.transform.position);
            child.transform.SetParent(player.transform);
            Collider extra = child.AddComponent<BoxCollider>();
            KnockbackZone knockback = zoneObject.AddComponent<KnockbackZone>();
            knockback.RequireLineOfSight = false;
            knockback.Strength = 5f;
            zoneObject.SendMessage("OnTriggerEnter", controller);
            zoneObject.SendMessage("OnTriggerEnter", extra);
            zoneObject.SendMessage("OnTriggerExit", extra);
            zoneObject.SendMessage("FixedUpdate");
            CharacterMovementComponent.CharacterMoveInput input =
                (CharacterMovementComponent.CharacterMoveInput)CallMovement(movement, "CreateMoveInput");
            Assert.That(input.ExternalVelocityChange.y, Is.EqualTo(5f));
            zoneObject.SendMessage("OnTriggerExit", controller);
            zoneObject.SendMessage("OnTriggerEnter", controller);
            input = (CharacterMovementComponent.CharacterMoveInput)CallMovement(movement, "CreateMoveInput");
            Assert.That(input.ExternalVelocityChange.y, Is.EqualTo(5f));
        }
        [Test]
        public void WindAcceleratesOncePerBodyWithCompoundColliders()
        {
            UnscriptedEngine.Gameplay.WindZone wind = zoneObject.AddComponent<UnscriptedEngine.Gameplay.WindZone>();
            wind.RequireLineOfSight = false;
            wind.Strength = 10f;
            Enter();
            GameObject child = CreateObject("Second Collider", body.position);
            child.transform.SetParent(body.transform);
            zoneObject.SendMessage("OnTriggerEnter", child.AddComponent<SphereCollider>());
            Step();
            Assert.That(body.linearVelocity.y, Is.EqualTo(0.2f).Within(0.001f));
        }

        [TestCase(3f, -1f)]
        [TestCase(-3f, 1f)]
        public void SuctionPullsTowardPlaneFromEitherSide(float height, float sign)
        {
            SuctionZone suction = zoneObject.AddComponent<SuctionZone>();
            suction.RequireLineOfSight = false;
            body.position = Vector3.up * height;
            Enter(); Step();
            Assert.That(body.linearVelocity.y * sign, Is.GreaterThan(0f));
        }

        [Test]
        public void KnockbackIsImmediateAndResetsAfterExit()
        {
            KnockbackZone knockback = zoneObject.AddComponent<KnockbackZone>();
            knockback.RequireLineOfSight = false;
            knockback.Strength = 4f;
            Enter();
            physics.Simulate(0.02f);
            Assert.That(body.linearVelocity.y, Is.EqualTo(4f).Within(0.001f));
            Step();
            Assert.That(body.linearVelocity.y, Is.EqualTo(4f).Within(0.001f));
            zoneObject.SendMessage("OnTriggerExit", contact);
            Enter(); physics.Simulate(0.02f);
            Assert.That(body.linearVelocity.y, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void WallBlocksWindUntilLineOfSightIsDisabled()
        {
            UnscriptedEngine.Gameplay.WindZone wind = zoneObject.AddComponent<UnscriptedEngine.Gameplay.WindZone>();
            CreateObject("Wall", Vector3.up * 1.5f).AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Enter(); Step();
            Assert.That(body.linearVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            wind.RequireLineOfSight = false;
            Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void RotatedSourceControlsDirectionAndKinematicBodiesAreIgnored()
        {
            UnscriptedEngine.Gameplay.WindZone wind = zoneObject.AddComponent<UnscriptedEngine.Gameplay.WindZone>();
            wind.RequireLineOfSight = false;
            wind.SourcePlane = CreateObject("Plane", Vector3.zero).transform;
            wind.SourcePlane.rotation = Quaternion.Euler(0f, 0f, -90f);
            Assert.That(Vector3.Dot(wind.WorldDirection, Vector3.right), Is.GreaterThan(0.999f));
            body.isKinematic = true;
            Enter(); Step();
            Assert.That(body.position, Is.EqualTo(Vector3.up * 3f));
        }
    }
}
