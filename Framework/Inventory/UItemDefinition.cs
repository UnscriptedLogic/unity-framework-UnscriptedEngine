using UnityEngine;
using Framework.GameplayTag;

namespace Framework.Inventory
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Unscripted Engine/Inventory/Item Definition")]
    public sealed class UItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStackSize = 64;
        [SerializeField, Min(0f)] private float weight;
        [SerializeField] private GameplayTagContainer tags = new();
        [SerializeField] private bool canBePlaced;
        [SerializeField] private GameObject droppedPrefab;
        [SerializeField] private GameObject placedPrefab;

        public string ItemId => itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int MaxStackSize => Mathf.Max(1, maxStackSize);
        public float Weight => weight;
        public GameplayTagContainer Tags => tags;
        public bool CanBePlaced => canBePlaced && placedPrefab != null;
        public GameObject DroppedPrefab => droppedPrefab;
        public GameObject PlacedPrefab => placedPrefab;

        private void OnValidate()
        {
            itemId = itemId?.Trim();
            maxStackSize = Mathf.Max(1, maxStackSize);
            weight = Mathf.Max(0f, weight);
        }
    }
}
