using UnityEngine;
namespace UnscriptedEngine {
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
