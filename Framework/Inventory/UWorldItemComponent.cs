using Framework.Components;
using UnityEngine;

namespace Framework.Inventory
{
    public sealed class UWorldItemComponent : UObjectComponent
    {
        [SerializeField] private UItemDefinition definition;
        [SerializeField] private UItemStack placedState;
        public UItemDefinition Definition => definition;
        public UItemStack PlacedState => placedState;

        public void Initialize(UItemStack source)
        {
            definition = source?.Definition;
            placedState = source?.Clone(1) ?? UItemStack.Empty();
        }

        public UItemStack CreatePickupStack() => placedState?.Clone(1) ?? new UItemStack(definition, 1);
    }
}
