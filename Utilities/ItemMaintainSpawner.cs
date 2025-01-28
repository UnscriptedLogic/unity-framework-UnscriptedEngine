using System.Collections.Generic;
using UnityEngine;

namespace General.Spawners
{
    public class ItemMaintainSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int count;
        [SerializeField] private List<GameObject> worldItems;

        private void Start()
        {
            for (int i = worldItems.Count; i < count; i++)
            {
                GameObject item = Instantiate(prefab, transform.position, Quaternion.identity);
                worldItems.Add(item);
            }
        }

        private void Update()
        {
            for (int i = worldItems.Count - 1; i >= 0; i--)
            {
                if (worldItems[i] == null)
                {
                    worldItems.RemoveAt(i);
                    GameObject newItem = Instantiate(prefab, transform.position, Quaternion.identity);
                    worldItems.Add(newItem);
                }
            }
        }
    }

}