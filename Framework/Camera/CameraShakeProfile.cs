using UnityEngine;

namespace UnscriptedEngine
{
    [CreateAssetMenu(menuName = "UnscriptedEngine/Camera Shake/Item Shake Profile")]
    public sealed class CameraShakeProfile : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float duration = 0.14f;
        [SerializeField] private Vector3 rotationDegrees = new(-2f, 0f, 0.5f);
        [SerializeField] private AnimationCurve envelope = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float Duration => duration;
        public Vector3 RotationDegrees => rotationDegrees;
        public AnimationCurve Envelope => envelope;
    }
}
