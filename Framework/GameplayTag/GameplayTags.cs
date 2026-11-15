using UnityEngine;

namespace Framework.GameplayTag
{
    /// <summary>Owns gameplay tags shared by this object and its child colliders.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unscripted Engine/Gameplay/Gameplay Tags")]
    public sealed class GameplayTags : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tags owned by this object. Child tags also match their parent tags.")]
        private GameplayTagContainer tags = new GameplayTagContainer();

        public GameplayTagContainer Tags => tags;
    }
}
