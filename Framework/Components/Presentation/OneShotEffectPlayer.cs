using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnscriptedEngine
{
    [AddComponentMenu("Unscripted Engine/Presentation/One Shot Effect Player")]
    public sealed class OneShotEffectPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("All assigned effect prefabs spawn together at the playback position and rotation.")]
        private GameObject[] effectPrefabs = Array.Empty<GameObject>();
        [SerializeField, Min(0.01f)] private float effectLifetime = 5f;
        [SerializeField, Tooltip("Play at this object when it is destroyed unless an explicit effect already played.")]
        private bool playOnDestroy;
        private bool played;

        public void Play() => PlayAt(transform.position, transform.rotation);

        public void PlayAt(Vector3 position, Quaternion rotation)
        {
            Scene scene = gameObject.scene;
            if (!Application.isPlaying || !scene.isLoaded) return;
            played = true;

            float lifetime = Mathf.Max(0.01f, effectLifetime);
            foreach (GameObject prefab in effectPrefabs)
            {
                if (prefab == null) continue;

                GameObject effect = Instantiate(prefab, position, rotation);
                SceneManager.MoveGameObjectToScene(effect, scene);
                Destroy(effect, lifetime);
            }
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
