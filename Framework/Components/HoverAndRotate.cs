using UnityEngine;

namespace Framework.Components
{
    /// <summary>
    /// Makes an object hover up and down while rotating continuously.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoverAndRotate : MonoBehaviour
    {
        [Header("Hover")]
        [Min(0f)]
        [SerializeField] private float hoverHeight = 0.25f;
        [Min(0f)]
        [SerializeField] private float hoverCyclesPerSecond = 1f;

        [Header("Rotation")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float rotationDegreesPerSecond = 90f;

        private Vector3 initialLocalPosition;
        private float hoverTime;

        private void Awake()
        {
            initialLocalPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            initialLocalPosition = transform.localPosition;
            hoverTime = 0f;
        }

        private void OnValidate()
        {
            hoverHeight = Mathf.Max(0f, hoverHeight);
            hoverCyclesPerSecond = Mathf.Max(0f, hoverCyclesPerSecond);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            hoverTime += deltaTime;

            float hoverOffset = hoverHeight * Mathf.Sin(hoverTime * hoverCyclesPerSecond * Mathf.PI * 2f);
            transform.localPosition = initialLocalPosition + Vector3.up * hoverOffset;

            if (rotationAxis.sqrMagnitude > 0f && !Mathf.Approximately(rotationDegreesPerSecond, 0f))
            {
                Quaternion rotation = Quaternion.AngleAxis(
                    rotationDegreesPerSecond * deltaTime,
                    rotationAxis.normalized);
                transform.localRotation = transform.localRotation * rotation;
            }
        }
    }
}
