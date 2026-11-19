using UnityEngine;
using UnityEngine.SceneManagement;
using UnscriptedEngine.Audio;

namespace UnscriptedEngine
{
    [AddComponentMenu("Unscripted Engine/Presentation/One Shot Effect Player")]
    public sealed class OneShotEffectPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional visual prefab. Leave empty for sound only.")] private GameObject effectPrefab;
        [SerializeField, Min(0.01f)] private float effectLifetime = 5f;
        [SerializeField, Tooltip("Optional spatial sound. Leave empty for visuals only.")] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Tooltip("Play at this object when it is destroyed unless an explicit effect already played.")]
        private bool playOnDestroy;
        private bool played;

        public void Play() => PlayAt(transform.position, transform.rotation);

        public void PlayAt(Vector3 position, Quaternion rotation)
        {
            Scene scene = gameObject.scene;
            if (!Application.isPlaying || !scene.isLoaded) return;
            played = true;

            if (effectPrefab != null)
            {
                GameObject effect = Instantiate(effectPrefab, position, rotation);
                SceneManager.MoveGameObjectToScene(effect, scene);
                Destroy(effect, Mathf.Max(0.01f, effectLifetime));
            }
            if (clip != null)
                AudioRuntime.Play(clip, AudioCategories.SoundEffects, volume, position: position);
        }

        private void OnApplicationQuit() => played = true;

        private void OnDestroy() => CompleteDestruction(playLocally: true);

        public void CompleteDestruction(bool playLocally)
        {
            if (playLocally && playOnDestroy && !played) Play();
            played = true;
        }
    }
}
