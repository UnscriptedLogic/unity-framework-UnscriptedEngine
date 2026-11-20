using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnscriptedEngine.Editor;

namespace UnscriptedEngine.Tests
{
    public class ItemEquipPoseTests
    {
        private const string SettingsPath = "ProjectSettings/UnscriptedEngineItemPoses.asset";
        private byte[] originalFile;
        private string originalSettings;
        private ItemDataSO item;

        [SetUp]
        public void Setup()
        {
            // Preserve the user's saved presets in memory and on disk.
            originalFile = File.Exists(SettingsPath) ? File.ReadAllBytes(SettingsPath) : null;
            originalSettings = EditorJsonUtility.ToJson(ItemEquipPosePresets.instance);
            EditorJsonUtility.FromJsonOverwrite("{\"hasLeftHand\":false,\"hasRightHand\":false}", ItemEquipPosePresets.instance);
            item = ScriptableObject.CreateInstance<ItemDataSO>();
            item.fragments.Add(new UItemFragEquip());
        }

        [TearDown]
        public void Cleanup()
        {
            Undo.ClearUndo(item);
            Object.DestroyImmediate(item);
            EditorJsonUtility.FromJsonOverwrite(originalSettings, ItemEquipPosePresets.instance);

            if (originalFile == null) File.Delete(SettingsPath);
            else File.WriteAllBytes(SettingsPath, originalFile);
        }

        [Test]
        public void PresetsStartUnsetAndStoreHandsIndependently()
        {
            ItemEquipPosePresets presets = ItemEquipPosePresets.instance;
            Assert.That(presets.TryGet(ItemHandPreset.LeftHand, out _, out _), Is.False);
            Assert.That(presets.TryGet(ItemHandPreset.RightHand, out _, out _), Is.False);

            Vector3 leftPosition = new Vector3(-0.2f, 0.3f, 0.4f);
            Vector3 leftRotation = new Vector3(10f, 20f, 30f);
            presets.SavePose(ItemHandPreset.LeftHand, leftPosition, leftRotation);
            presets.SavePose(ItemHandPreset.RightHand, Vector3.one, Vector3.up * 90f);

            Assert.That(presets.TryGet(ItemHandPreset.LeftHand, out Vector3 position, out Vector3 rotation), Is.True);
            Assert.That(position, Is.EqualTo(leftPosition));
            Assert.That(rotation, Is.EqualTo(leftRotation));
            Assert.That(presets.TryGet(ItemHandPreset.RightHand, out position, out rotation), Is.True);
            Assert.That(position, Is.EqualTo(Vector3.one));
            Assert.That(rotation, Is.EqualTo(Vector3.up * 90f));
            Assert.That(new FileInfo(SettingsPath).Length, Is.GreaterThan(0));
        }

        [Test]
        public void ApplyingPoseSupportsUndoAndDoesNotChangeOtherFragments()
        {
            item.fragments.Add(new UItemFragEquip());
            Undo.IncrementCurrentGroup();

            using (SerializedObject serialized = new SerializedObject(item))
            {
                SerializedProperty fragment = serialized.FindProperty("fragments").GetArrayElementAtIndex(0);
                ItemEquipPosePresets.ApplyPose(fragment, Vector3.right, Vector3.up * 45f);
            }

            Assert.That(((UItemFragEquip)item.fragments[0]).AnchorPosition, Is.EqualTo(Vector3.right));
            Assert.That(((UItemFragEquip)item.fragments[0]).AnchorRotation, Is.EqualTo(Vector3.up * 45f));
            Assert.That(((UItemFragEquip)item.fragments[1]).AnchorPosition, Is.EqualTo(Vector3.zero));

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(((UItemFragEquip)item.fragments[0]).AnchorPosition, Is.EqualTo(Vector3.zero));
            Assert.That(((UItemFragEquip)item.fragments[0]).AnchorRotation, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MirroredHandReflectsPositionAndRotation()
        {
            Vector3 position = new Vector3(0.35f, -0.3f, 0.1f);
            Vector3 angles = new Vector3(20f, 35f, -15f);
            using (SerializedObject serialized = new SerializedObject(item))
                ItemEquipPosePresets.ApplyPose(serialized.FindProperty("fragments").GetArrayElementAtIndex(0), position, angles);

            var visual = new GameObject("Held visual");
            try
            {
                var fragment = (UItemFragEquip)item.fragments[0];
                fragment.ApplyVisualPose(visual.transform, true);
                Assert.That(visual.transform.localPosition, Is.EqualTo(new Vector3(-position.x, position.y, position.z)));
                Vector3 forward = Quaternion.Euler(angles) * Vector3.forward;
                forward.x = -forward.x;
                Assert.That(Vector3.Distance(visual.transform.localRotation * Vector3.forward, forward), Is.LessThan(0.0001f));
                Assert.That(visual.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void ApplyingPresetCopiesValuesWithoutLiveLink()
        {
            ItemEquipPosePresets.instance.SavePose(ItemHandPreset.LeftHand, Vector3.left, Vector3.forward);
            ItemEquipPosePresets.instance.TryGet(ItemHandPreset.LeftHand, out Vector3 position, out Vector3 rotation);

            using (SerializedObject serialized = new SerializedObject(item))
            {
                ItemEquipPosePresets.ApplyPose(serialized.FindProperty("fragments").GetArrayElementAtIndex(0), position, rotation);
            }

            ItemEquipPosePresets.instance.SavePose(ItemHandPreset.LeftHand, Vector3.up, Vector3.zero);
            Assert.That(((UItemFragEquip)item.fragments[0]).AnchorPosition, Is.EqualTo(Vector3.left));
            Assert.That(((UItemFragEquip)item.fragments[0]).AnchorRotation, Is.EqualTo(Vector3.forward));
        }
    }
}
