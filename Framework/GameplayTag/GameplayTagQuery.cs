using UnityEngine;

namespace Framework.GameplayTag
{
    [CreateAssetMenu(fileName = "GameplayTagQuery", menuName = "UnscriptedEngine/Gameplay Tags/Query")]
    public sealed class GameplayTagQuery : ScriptableObject
    {
        [SerializeField] private GameplayTag[] requiredTags;
        [SerializeField] private GameplayTag[] anyTags;
        [SerializeField] private GameplayTag[] blockedTags;

        public bool Evaluate(GameplayTagContainer container)
        {
            if (container == null)
            {
                return false;
            }

            return container.HasAll(requiredTags)
                && (anyTags == null || anyTags.Length == 0 || container.HasAny(anyTags))
                && container.HasNone(blockedTags);
        }
    }
}
