using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine.Item;

namespace UnscriptedEngine
{
    public sealed class InventoryLootRelease : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Inventory to release. Server/offline only.")]
        private UInventoryComponent inventory;
        [SerializeField]
        [Tooltip("Spawn origin; defaults to this transform.")]
        private Transform origin;
        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum pickups per call; excess contents remain stored.")]
        private int maxPickups = 16;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Outward launch speed in metres per second.")]
        private float outwardSpeed = 3f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Upward launch speed in metres per second.")]
        private float upwardSpeed = 4f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Random angular speed in radians per second.")]
        private float spinSpeed = 4f;
        [SerializeField]
        [Min(0f)]
        [Tooltip("Horizontal spawn radius in metres; place beyond chest collider.")]
        private float spawnRadius = 0.5f;
        [SerializeField]
        [Tooltip("Spawn individual items instead of one pickup per stack.")]
        private bool individualItems;
        [Header("Debug")]
        [SerializeField]
        [Tooltip("Show spawn radius when selected.")]
        private bool showGizmos = true;
        private bool releasing;

        public int Release()
        {
            if (releasing || inventory == null) return 0;
            inventory.Initialize();
            if (inventory.Items == null) return 0;
            releasing = true;
            int released = 0;
            try
            {
                List<int> indices = new List<int>(inventory.Items.Keys);
                indices.Sort();
                foreach (int index in indices)
                {
                    while (released < maxPickups && inventory.Items.TryGetValue(index, out InventorySlot slot))
                    {
                        Vector2 radial = Random.insideUnitCircle;
                        Vector3 direction = new Vector3(radial.x, 0f, radial.y).normalized;
                        Transform spawn = origin != null ? origin : transform;
                        int quantity = individualItems ? 1 : slot.Quantity;
                        UInventoryComponent.OperationResult result = inventory.TryDropItem(index,
                            spawn.position + direction * spawnRadius, spawn.rotation, out UWorldItem pickup, quantity);
                        if (!result.Success) break;
                        released++;
                        if (pickup != null && pickup.TryGetComponent(out Rigidbody body))
                        {
                            body.isKinematic = false;
                            body.useGravity = true;
                            body.linearVelocity = direction * outwardSpeed + Vector3.up * upwardSpeed;
                            body.angularVelocity = Random.insideUnitSphere * spinSpeed;
                        }
                    }
                    if (released >= maxPickups) break;
                }
            }
            finally { releasing = false; }
            return released;
        }

        public void BurstOpen() => Release();

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            Vector3 center = origin != null ? origin.position : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, spawnRadius);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(center + Vector3.up * spawnRadius, "Loot spawn radius");
#endif
        }
    }
}
