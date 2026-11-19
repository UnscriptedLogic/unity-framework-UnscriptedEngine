using UnityEngine;

namespace UnscriptedEngine.Audio
{
    /// <summary>Plays a configured one-shot through shared audio on Awake or from a UnityEvent.</summary>
    [AddComponentMenu("Unscripted Engine/Audio/Audio Event Player")]
    public sealed class AudioEventPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base volume, multiplied by the category and master volumes.")]
        private float volume = 1f;

        [SerializeField]
        [Tooltip("Shared audio category ID, using lowercase letters, digits, and hyphens.")]
        private string category = AudioCategories.SoundEffects;

        [SerializeField]
        [Tooltip("Play in 3D at this object's position. Disable for 2D playback. The sound does not follow the object.")]
        private bool positional = true;

        [SerializeField, Min(0f)]
        [Tooltip("For positional audio, fade to silence at this distance in world units. Zero uses Unity's default falloff.")]
        private float hearingRange;

        [SerializeField, Tooltip("Play once when this component awakens. Enable on spawned effects.")]
        private bool playOnAwake;

        private void Awake()
        {
            if (playOnAwake) Play();
        }

        /// <summary>Starts an independent one-shot. Suitable for Inspector UnityEvent listeners.</summary>
        public void Play()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || clip == null) return;

            AudioRuntime.Play(clip, category, volume, loop: false,
                position: positional ? (Vector3?)transform.position : null, hearingRange: hearingRange);
        }
    }
}
