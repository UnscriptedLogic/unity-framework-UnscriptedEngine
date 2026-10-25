using UnityEditor;
using UnityEngine;

namespace UnscriptedEngine.Editor
{
    public enum ItemHandPreset
    {
        LeftHand,
        RightHand
    }

    [FilePath("ProjectSettings/UnscriptedEngineItemPoses.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class ItemEquipPosePresets : ScriptableSingleton<ItemEquipPosePresets>
    {
        [SerializeField] private bool hasLeftHand;
        [SerializeField] private Vector3 leftPosition;
        [SerializeField] private Vector3 leftRotation;

        [SerializeField] private bool hasRightHand;
        [SerializeField] private Vector3 rightPosition;
        [SerializeField] private Vector3 rightRotation;

        public bool TryGet(ItemHandPreset hand, out Vector3 position, out Vector3 rotation)
        {
            bool left = hand == ItemHandPreset.LeftHand;
            position = left ? leftPosition : rightPosition;
            rotation = left ? leftRotation : rightRotation;

            return left ? hasLeftHand : hasRightHand;
        }

        public void SavePose(ItemHandPreset hand, Vector3 position, Vector3 rotation)
        {
            if (hand == ItemHandPreset.LeftHand)
            {
                hasLeftHand = true;
                leftPosition = position;
                leftRotation = rotation;
            }
            else
            {
                hasRightHand = true;
                rightPosition = position;
                rightRotation = rotation;
            }

            Save(true);
        }

        public static void ApplyPose(SerializedProperty fragment, Vector3 position, Vector3 rotation)
        {
            SerializedProperty anchorPosition = fragment.FindPropertyRelative("anchorPosition");
            SerializedProperty anchorRotation = fragment.FindPropertyRelative("anchorRotation");
            anchorPosition.vector3Value = position;
            anchorRotation.vector3Value = rotation;

            Undo.SetCurrentGroupName("Apply item anchor pose");
            fragment.serializedObject.ApplyModifiedProperties();
        }
    }
}
