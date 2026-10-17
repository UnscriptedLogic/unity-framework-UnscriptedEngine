using UnityEngine;
using System;
namespace UnscriptedEngine {
public enum PlayerInputPhase
{
    Started,
    Performed,
    Canceled
}

public abstract class PlayerInputEvent : ICancellableEvent
{
    protected PlayerInputEvent(UController controller)
    {
        Controller = controller;
    }

    public UController Controller { get; }
    public bool IsCancelled { get; private set; }

    public void Cancel()
    {
        IsCancelled = true;
    }
}

/// <summary>
/// One EventBus message for any configured Input System action. ActionKey uses
/// "Map/Action" so listeners can filter without declaring an event class per action.
/// </summary>
public sealed class PlayerActionInputEvent : PlayerInputEvent
{
    public PlayerActionInputEvent(UController controller, string actionMap, string actionName,
        Guid actionId, PlayerInputPhase phase, object value, bool isPressed,
        bool isPointerDelta, bool isGameplayBlocked, bool isSynthetic = false,
        string sourceActionMap = null, string sourceActionName = null) : base(controller)
    {
        ActionMap = actionMap ?? string.Empty;
        ActionName = actionName ?? string.Empty;
        ActionId = actionId;
        Phase = phase;
        Value = value;
        IsPressed = isPressed;
        IsPointerDelta = isPointerDelta;
        IsGameplayBlocked = isGameplayBlocked;
        IsSynthetic = isSynthetic;
        SourceActionMap = sourceActionMap ?? ActionMap;
        SourceActionName = sourceActionName ?? ActionName;
    }

    public string ActionMap { get; }
    public string ActionName { get; }
    public string ActionKey => $"{ActionMap}/{ActionName}";
    public Guid ActionId { get; }
    public PlayerInputPhase Phase { get; }
    public object Value { get; }
    public bool IsPressed { get; }
    public bool IsPointerDelta { get; }
    public bool IsGameplayBlocked { get; }
    public bool IsSynthetic { get; }
    public string SourceActionMap { get; }
    public string SourceActionName { get; }
    public bool IsPerformed => Phase == PlayerInputPhase.Performed;
    public bool IsActionCanceled => Phase == PlayerInputPhase.Canceled;
    public bool IsPerformedOrCanceled => IsPerformed || IsActionCanceled;

    public bool IsAction(string actionMap, string actionName)
    {
        return string.Equals(ActionMap, actionMap, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ActionName, actionName, StringComparison.OrdinalIgnoreCase);
    }

    public T ReadValue<T>()
    {
        if (Value is T value) return value;
        throw new InvalidCastException($"Action {ActionKey} carries {Value?.GetType().Name ?? "no value"}, not {typeof(T).Name}.");
    }

    public bool TryReadValue<T>(out T value)
    {
        if (Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}

public class PlayerMoveInputEvent : PlayerInputEvent
{
    public PlayerMoveInputEvent(UController controller, Vector2 input) : base(controller)
    {
        Input = input;
    }

    public Vector2 Input { get; }
}

public class PlayerLookInputEvent : PlayerInputEvent
{
    public PlayerLookInputEvent(UController controller, Vector2 input, bool isPointerDelta) : base(controller)
    {
        Input = input;
        IsPointerDelta = isPointerDelta;
    }

    public Vector2 Input { get; private set; }
    public bool IsPointerDelta { get; }

    public void OverrideInput(Vector2 input)
    {
        Input = input;
    }
}

public class PlayerSprintInputEvent : PlayerInputEvent
{
    public PlayerSprintInputEvent(UController controller, bool isSprinting) : base(controller)
    {
        IsSprinting = isSprinting;
    }

    public bool IsSprinting { get; }
}

public class PlayerJumpInputEvent : PlayerInputEvent
{
    public PlayerJumpInputEvent(UController controller, bool isJumping) : base(controller)
    {
        IsJumping = isJumping;
    }

    public bool IsJumping { get; }
}

public class PlayerInteractInputEvent : PlayerInputEvent
{
    public bool IsHeld { get; }

    public PlayerInteractInputEvent(UController controller, bool isHeld = true) : base(controller)
    {
        IsHeld = isHeld;
    }
}


}
