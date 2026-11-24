using System.Linq;
using Framework.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ProceduralTreeTests
{
    [Test]
    public void UniformLeafScaleKeepsEachSeededWidthAndAppliesItToAllAxes()
    {
        GameObject root = new GameObject("Tree");
        try
        {
            ProceduralTree tree = root.AddComponent<ProceduralTree>();
            tree.Generate();
            Vector3[] originalSizes = GetLeafScales(root);
            Assert.That(originalSizes.Length, Is.GreaterThan(1));
            Assert.That(originalSizes[0].x, Is.Not.EqualTo(originalSizes[1].x));
            Assert.That(originalSizes[0].y, Is.Not.EqualTo(originalSizes[0].x));

            using (SerializedObject settings = new SerializedObject(tree))
            {
                settings.FindProperty("uniformLeafScale").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }

            tree.Generate();
            Vector3[] expectedSizes = originalSizes.Select(size => Vector3.one * size.x).ToArray();
            Assert.That(GetLeafScales(root), Is.EqualTo(expectedSizes));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TrunkCrownPlacesLeavesAtEachTipWithoutSideBranches(bool split)
    {
        GameObject root = new GameObject("Tree");
        try
        {
            ProceduralTree tree = root.AddComponent<ProceduralTree>();
            using (SerializedObject settings = new SerializedObject(tree))
            {
                settings.FindProperty("branchLevels").intValue = 0;
                settings.FindProperty("splitTrunkTop").boolValue = split;
                settings.FindProperty("leavesAtTrunkTip").boolValue = true;
                settings.FindProperty("leafBlocksPerTip").intValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }

            tree.Generate();
            Transform crown = root.transform.GetChild(0);
            if (split)
            {
                Transform firstTip = crown.Find("Trunk Fork Tip 1");
                Transform secondTip = crown.Find("Trunk Fork Tip 2");
                Assert.That(firstTip, Is.Not.Null);
                Assert.That(secondTip, Is.Not.Null);
                Assert.That(firstTip.Find("Leaf Block 01").position,
                    Is.Not.EqualTo(secondTip.Find("Leaf Block 01").position));
            }
            else
            {
                Transform tipBlock = crown.Find("Trunk Block 010");
                Vector3 tip = tipBlock.position + tipBlock.up * (tipBlock.localScale.y * 0.5f);
                Assert.That(Vector3.Distance(crown.Find("Leaf Block 01").position, tip), Is.LessThan(0.0001f));
            }

            Assert.That(GetLeafScales(root).Length, Is.EqualTo(split ? 2 : 1));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [TestCase(3f, 3)]
    [TestCase(10f, 0)]
    public void BranchFreeBaseKeepsSideBranchAttachmentsAboveClearance(float clearance, int expectedBranches)
    {
        GameObject root = new GameObject("Tree");
        try
        {
            ProceduralTree tree = root.AddComponent<ProceduralTree>();
            using (SerializedObject settings = new SerializedObject(tree))
            {
                settings.FindProperty("curve").floatValue = 0f;
                settings.FindProperty("branchFreeBaseHeight").floatValue = clearance;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }

            tree.Generate();
            Transform[] branches = root.transform.GetChild(0).Cast<Transform>()
                .Where(child => child.name.StartsWith("Branch ", System.StringComparison.Ordinal))
                .ToArray();
            Assert.That(branches.Length, Is.EqualTo(expectedBranches));
            foreach (Transform branch in branches)
            {
                Transform firstBlock = branch.GetChild(0);
                Vector3 attachment = firstBlock.position - firstBlock.up * (firstBlock.localScale.y * 0.5f);
                Assert.That(attachment.y, Is.GreaterThan(clearance));
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Vector3[] GetLeafScales(GameObject root)
    {
        return root.GetComponentsInChildren<Transform>()
            .Where(child => child.name.StartsWith("Leaf Block ", System.StringComparison.Ordinal))
            .Select(child => child.localScale)
            .ToArray();
    }
}
