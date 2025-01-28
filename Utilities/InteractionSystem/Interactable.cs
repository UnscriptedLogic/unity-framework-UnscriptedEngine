using UnityEngine;
using UnityEngine.Events;

namespace InteractionSystem
{
    public class Interactable : MonoBehaviour
    {
        [Header("Interactable Settings")]
        [SerializeField] private string description;
        [SerializeField] private float confirmTime;

        public string Description => description;
        public float ConfirmTime => confirmTime;

        public UnityEvent<GameObject> OnInteract;
    } 
}