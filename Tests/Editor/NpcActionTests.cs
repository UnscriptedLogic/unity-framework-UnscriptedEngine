using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class NpcActionTests
    {
        private sealed class Navigation : INpcNavigation
        {
            public Vector3 Position { get; set; }
            public NavMeshMovementComponent.NavigationStatus Status { get; set; }
            public bool AcceptMove { get; set; } = true;
            public int Stops { get; private set; }
            public bool MoveTo(Vector3 destination, float stoppingDistance)
            {
                Status = NavMeshMovementComponent.NavigationStatus.Moving;
                return AcceptMove;
            }
            public void Stop() { Stops++; Status = NavMeshMovementComponent.NavigationStatus.Idle; }
        }

        [Test]
        public void IdleRestartGetsFullDurationAndNegativeTimeDoesNotAdvance()
        {
            NpcIdleAction idle = new NpcIdleAction();
            idle.Configure(2f);
            idle.Start();
            Assert.That(idle.Tick(-1f), Is.EqualTo(NpcActionStatus.Running));
            Assert.That(idle.Tick(1f), Is.EqualTo(NpcActionStatus.Running));
            idle.Cancel();
            idle.Start();
            Assert.That(idle.Tick(1f), Is.EqualTo(NpcActionStatus.Running));
            Assert.That(idle.Tick(1f), Is.EqualTo(NpcActionStatus.Succeeded));
        }

        [Test]
        public void RejectedMoveFailsAndStopsNavigation()
        {
            Navigation navigation = new Navigation { AcceptMove = false };
            NpcMoveAction action = CreateMove(navigation);
            action.Start();
            Assert.That(action.Tick(0f), Is.EqualTo(NpcActionStatus.Failed));
            Assert.That(navigation.Stops, Is.EqualTo(1));
        }

        [Test]
        public void ArrivalSucceedsAndTerminalTicksDoNotRepeatStop()
        {
            Navigation navigation = new Navigation();
            NpcMoveAction action = CreateMove(navigation);
            action.Start();
            navigation.Status = NavMeshMovementComponent.NavigationStatus.Arrived;
            Assert.That(action.Tick(0.1f), Is.EqualTo(NpcActionStatus.Succeeded));
            Assert.That(action.Tick(0.1f), Is.EqualTo(NpcActionStatus.Succeeded));
            Assert.That(navigation.Stops, Is.EqualTo(1));
        }

        [Test]
        public void VerticalMotionDoesNotHideGroundedStall()
        {
            Navigation navigation = new Navigation();
            NpcMoveAction action = CreateMove(navigation);
            action.Start();
            navigation.Position = Vector3.up * 10f;
            Assert.That(action.Tick(2f), Is.EqualTo(NpcActionStatus.Failed));
        }

        [Test]
        public void ClimbingCanCountVerticalProgressAndGroundedConfigurationResetsIt()
        {
            Navigation navigation = new Navigation();
            NpcMoveAction action = new NpcMoveAction(navigation);
            action.Configure(Vector3.up * 10f, 0.3f, 20f, 2f, 0.1f, true);
            action.Start();
            for (int index = 1; index <= 4; index++)
            {
                navigation.Position = Vector3.up * index;
                Assert.That(action.Tick(1f), Is.EqualTo(NpcActionStatus.Running));
            }
            action.Configure(Vector3.up * 10f, 0.3f, 20f, 2f, 0.1f);
            action.Start();
            navigation.Position += Vector3.up;
            Assert.That(action.Tick(2f), Is.EqualTo(NpcActionStatus.Failed));
        }

        [Test]
        public void ProgressResetsStallButCannotExtendOverallDeadline()
        {
            Navigation navigation = new Navigation();
            NpcMoveAction action = CreateMove(navigation);
            action.Start();
            for (int index = 1; index < 5; index++)
            {
                navigation.Position = Vector3.right * index;
                Assert.That(action.Tick(1f), Is.EqualTo(NpcActionStatus.Running));
            }
            navigation.Position = Vector3.right * 5f;
            Assert.That(action.Tick(1f), Is.EqualTo(NpcActionStatus.Failed));
        }

        [Test]
        public void CancelStopsAndRestartClearsOldDeadlines()
        {
            Navigation navigation = new Navigation();
            NpcMoveAction action = CreateMove(navigation);
            action.Start();
            action.Tick(1.5f);
            action.Cancel();
            Assert.That(navigation.Status, Is.EqualTo(NavMeshMovementComponent.NavigationStatus.Idle));
            action.Start();
            Assert.That(action.Tick(1f), Is.EqualTo(NpcActionStatus.Running));
            navigation.Status = NavMeshMovementComponent.NavigationStatus.Failed;
            Assert.That(action.Tick(0f), Is.EqualTo(NpcActionStatus.Failed));
        }

        private static NpcMoveAction CreateMove(Navigation navigation)
        {
            NpcMoveAction action = new NpcMoveAction(navigation);
            action.Configure(Vector3.forward * 5f, 0.3f, 5f, 2f, 0.1f);
            return action;
        }
    }
}
