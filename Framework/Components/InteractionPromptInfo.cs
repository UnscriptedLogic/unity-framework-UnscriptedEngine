using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Optional presentation data beside an interactable. Does not grant interaction permission.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptInfo : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private string actionText = "Interact";
        [SerializeField] private Sprite icon;

        public string DisplayName => displayName;
        public string ActionText => actionText;
        public Sprite Icon => icon;
    }
}
