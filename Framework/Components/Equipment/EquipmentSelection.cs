using UnityEngine;
namespace UnscriptedEngine
{
    public static class EquipmentSelection
    {
        public static void SelectNumber(ItemEquipComponent equipment, int number)
        {
            if (equipment == null || number < 0 || number > 9) return;
            equipment.SelectSlot(number == 0 ? 9 : number - 1);
        }
        public static void Scroll(ItemEquipComponent equipment, float delta)
        {
            if (equipment == null || delta == 0 || float.IsNaN(delta)) return;
            int count = equipment.VisibleSlotCount;
            if (count == 0) return;
            int index = equipment.SelectedSlotIndex;
            equipment.SelectSlot(index < 0 ? 0 : (index + (delta > 0 ? -1 : 1) + count) % count);
        }
    }
}
