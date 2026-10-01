using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "Item")]
    public class ItemDataSO : ScriptableObject
    {
        [SerializeReference] public List<UItemFragment> fragments = new();
    }
}