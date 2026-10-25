using System.Collections.Generic;
using UnityEngine;

namespace UnscriptedEngine
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "Item")]
    public class ItemDataSO : ScriptableObject
    {
        [SerializeField, HideInInspector] private string networkId;
        public string NetworkId => networkId;
#if UNITY_EDITOR
        private void OnValidate()
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(path)) networkId = UnityEditor.AssetDatabase.AssetPathToGUID(path);
        }
#endif
        [SerializeReference] public List<UItemFragment> fragments = new();
    }
}
