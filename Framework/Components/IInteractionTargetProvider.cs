namespace UnscriptedEngine
{
    /// <summary>Shared targeting contract for raycast, proximity, or custom targeting.</summary>
    public interface IInteractionTargetProvider
    {
        UInteractableComponent FocusedInteractable { get; }
    }
}
