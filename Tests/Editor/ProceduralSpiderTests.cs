using Framework.Components;
using NUnit.Framework;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class ProceduralSpiderTests
    {
        private GameObject root;
        private GameObject surface;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (surface != null) Object.DestroyImmediate(surface);
        }

        [Test]
        public void RebuildUsesRequestedChainAndLeavesNoCollidersOrDuplicateBlocks()
        {
            ProceduralSpiderLeg leg = CreateLeg();
            leg.Configure(new[] { new SpiderLegSection(0.4f, 40f), new SpiderLegSection(0.7f, -50f), new SpiderLegSection(1f, -60f) }, null);
            leg.RebuildGeometry();
            leg.RebuildGeometry();
            Assert.That(leg.transform.childCount, Is.EqualTo(3));
            Assert.That(leg.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(leg.Reach, Is.EqualTo(2.1f).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FootStaysPlantedWhenBodyMovesAlongFloorOrWall(bool wall)
        {
            ProceduralSpiderLeg leg = CreateLeg();
            if (wall) root.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            Vector3 up = root.transform.up;
            surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.transform.rotation = root.transform.rotation;
            surface.transform.position = -up * 1.05f;
            surface.transform.localScale = new Vector3(10f, 0.1f, 10f);
            Physics.SyncTransforms();
            leg.RebuildGeometry();
            Vector3 planted = leg.FootPosition;
            Assert.That(Vector3.Dot(planted, up), Is.EqualTo(-1f).Within(0.001f));
            root.transform.position += root.transform.forward * 0.2f;
            leg.Tick(0.02f);
            Assert.That(Vector3.Distance(planted, leg.FootPosition), Is.LessThan(0.0001f));
            Transform lastBlock = leg.transform.GetChild(leg.SectionCount - 1);
            Vector3 tip = lastBlock.position + lastBlock.up * lastBlock.lossyScale.y * 0.5f;
            Assert.That(Vector3.Distance(tip, planted), Is.LessThan(0.02f));
            surface.transform.position += up * 0.1f;
            leg.Tick(0.02f);
            Assert.That(Vector3.Distance(leg.FootPosition, planted + up * 0.1f), Is.LessThan(0.001f));
        }

        [Test]
        public void StepFinishesAndReenableClearsTransientState()
        {
            ProceduralSpiderLeg leg = CreateLeg();
            surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.transform.position = Vector3.down * 1.05f;
            surface.transform.localScale = new Vector3(10f, 0.1f, 10f);
            Physics.SyncTransforms();
            leg.RebuildGeometry();
            root.transform.position += Vector3.forward * 0.5f;
            leg.Tick(0f);
            Assert.That(leg.TryBeginStep(), Is.True);
            leg.Tick(1f);
            Assert.That(leg.IsStepping, Is.False);
            Assert.That(leg.FootPosition.y, Is.EqualTo(-1f).Within(0.001f));
            root.transform.position += Vector3.forward * 0.5f;
            Assert.That(leg.TryBeginStep(), Is.True);
            leg.enabled = false;
            leg.enabled = true;
            Assert.That(leg.IsStepping, Is.False);
            leg.Tick(0f);
            Assert.That(float.IsNaN(leg.FootPosition.x), Is.False);
        }

        [Test]
        public void BrainDoesNotCollectLegsOwnedByNestedBrain()
        {
            ProceduralSpiderLeg leg = CreateLeg();
            ProceduralSpider brain = root.AddComponent<ProceduralSpider>();
            GameObject nested = new GameObject("Nested Spider");
            nested.transform.SetParent(root.transform);
            nested.AddComponent<ProceduralSpider>();
            nested.AddComponent<ProceduralSpiderLeg>();
            brain.CollectChildLegs();
            Assert.That(brain.Legs.Count, Is.EqualTo(1));
            Assert.That(brain.Legs[0], Is.SameAs(leg));
        }

        private ProceduralSpiderLeg CreateLeg()
        {
            root = new GameObject("Spider Test");
            return root.AddComponent<ProceduralSpiderLeg>();
        }
    }
}
