using System;
using UnityEngine;

namespace Framework.Components
{
    public class MovementVaultingComponent : UObjectComponent
    {
        [SerializeField] private Transform origin;

        private Vector3 VaultCheckOriginPoint => origin.position + origin.forward * 1f + Vector3.up * 1.0f;
        private float VaultCheckLength => 1.5f;
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(VaultCheckOriginPoint, 0.1f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawRay(VaultCheckOriginPoint, Vector3.down * VaultCheckLength);
        }

        private void OnValidate()
        {
            if (origin == null)
            {
                origin = transform;
            }
        }
    }
}