using UnityEngine;

namespace General.Spawners
{
    public class ItemTakeSpawner : MonoBehaviour
	{
        [SerializeField] private GameObject prefab;
        [SerializeField, Tooltip("Used to determine when to spawn the next one when it leaves the collider")] 
        private GameObject createdObject;

        private void Update()
        {
            if (createdObject == null)
            {
                createdObject = Instantiate(prefab, transform.position, transform.rotation);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject == createdObject)
            {
                createdObject = Instantiate(prefab, transform.position, transform.rotation);
            }
        }
    }
}