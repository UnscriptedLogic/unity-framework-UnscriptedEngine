using UnityEngine;

namespace UnscriptedEngine.Audio
{
    /// <summary>Client-side distance-based footsteps driven by CharacterMovementComponent.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMovementComponent))]
    [AddComponentMenu("Unscripted Engine/Audio/Character Footsteps")]
    public sealed class CharacterFootstepAudio : UObjectComponent
    {
        [SerializeField]
        [Tooltip("Movement state and simulation position used to schedule footsteps. Defaults to the same object.")]
        private CharacterMovementComponent movement;

        [SerializeField]
        [Tooltip("Footstep variations. Assign non-null clips; consecutive steps avoid the same array entry.")]
        private AudioClip[] clips = new AudioClip[0];

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Horizontal metres travelled between steps. Faster movement naturally increases cadence.")]
        private float stepDistance = 1.8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum horizontal speed in metres per second required to accumulate footsteps.")]
        private float minimumSpeed = 0.2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Base footstep volume, multiplied by the category and master volumes.")]
        private float volume = 0.45f;

        [SerializeField]
        [Tooltip("Shared audio category ID. Use entities for NPCs, or player for player characters.")]
        private string category = AudioCategories.Player;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw the configured step distance when this character is selected.")]
        private bool showGizmos;

        private Vector3 previousPosition;
        private float travelledDistance;
        private int previousClipIndex = -1;
        private bool hasPosition;

        protected override void Awake()
        {
            if (movement == null) movement = GetComponent<CharacterMovementComponent>();
            base.Awake();
        }

        protected override void BeginPlay()
        {
            base.BeginPlay();
            ResetFootsteps();
        }

        protected override void ClientTick(float deltaTime)
        {
            if (movement == null || !movement.isActiveAndEnabled || !movement.IsSpawned ||
                !movement.HasBegunPlay || !movement.IsGrounded || movement.IsFlying ||
                movement.IsMovementLocked || deltaTime <= 0f || Time.timeScale == 0f)
            {
                ResetFootsteps();
                return;
            }

            Vector3 position = movement.SimulationPosition;
            if (!hasPosition)
            {
                previousPosition = position;
                hasPosition = true;
                return;
            }

            Vector3 displacement = position - previousPosition;
            previousPosition = position;
            displacement.y = 0f;
            float distance = displacement.magnitude;
            float horizontalSpeedSquared = movement.Velocity.x * movement.Velocity.x +
                movement.Velocity.z * movement.Velocity.z;
            // Ignore large teleports/reconciliation jumps.
            if (distance > Mathf.Max(stepDistance * 2f, movement.MaxWalkSpeed * deltaTime * 3f))
            {
                travelledDistance = 0f;
                return;
            }

            if (AccumulateStepDistance(distance, horizontalSpeedSquared, deltaTime)) PlayFootstep(position);
        }

        private bool AccumulateStepDistance(float distance, float horizontalSpeedSquared, float deltaTime)
        {
            if (horizontalSpeedSquared < minimumSpeed * minimumSpeed)
            {
                travelledDistance = 0f;
                return false;
            }

            // SimulationPosition changes on fixed ticks, not every rendered frame.
            // Preserve progress while waiting for the next movement sample.
            if (distance < minimumSpeed * deltaTime) return false;
            travelledDistance += distance;
            if (travelledDistance < stepDistance) return false;
            travelledDistance %= stepDistance;
            return true;
        }

        private void PlayFootstep(Vector3 position)
        {
            if (clips == null || clips.Length == 0) return;
            int index = previousClipIndex < 0 || clips.Length == 1
                ? Random.Range(0, clips.Length)
                : (previousClipIndex + Random.Range(1, clips.Length)) % clips.Length;
            previousClipIndex = index;
            if (clips[index] == null) return;
            AudioRuntime.Play(clips[index], category, volume, position: position);
        }

        private void ResetFootsteps()
        {
            travelledDistance = 0f;
            previousClipIndex = -1;
            hasPosition = false;
        }

        protected override void OnDisable()
        {
            ResetFootsteps();
            base.OnDisable();
        }

        protected override void EndPlay() => ResetFootsteps();

        private void OnValidate()
        {
            stepDistance = Mathf.Max(0.1f, stepDistance);
            minimumSpeed = Mathf.Max(0f, minimumSpeed);
            volume = Mathf.Clamp01(volume);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            Vector3 end = transform.position + transform.forward * stepDistance;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, end);
            Gizmos.DrawWireSphere(end, 0.06f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(end, $"Footstep distance: {stepDistance:0.##} m");
#endif
        }
    }
}
