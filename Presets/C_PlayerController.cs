using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnscriptedEngine;

public class C_PlayerController : UController
{
    public event Action<Vector2> OnMovementInput;
    public event Action<bool> OnSpace;
    public event Action<bool> OnShift;
    public event Action<bool> OnInteract;
    public event Action<bool> OnMiddleMouse;
    public event Action<float> OnMouseScroll;

    protected bool initialized;

    protected virtual void OnEnable()
    {
        EnableInput(GameMode.InputContext);
    }

    protected virtual void OnDisable()
    {
        DisableInput(GameMode.InputContext);
    }

    protected override void EnableInput(InputActionAsset inputContext)
    {
        base.EnableInput(inputContext);

        inputContext.FindAction("Move").performed += OnMove;
        inputContext.FindAction("Move").canceled += OnMove;
        inputContext.FindAction("Space").performed += OnSpacePerformed;
        inputContext.FindAction("Space").canceled += OnSpaceCancelled;
        inputContext.FindAction("Interact").performed += OnInteractPerformed;
        inputContext.FindAction("Interact").canceled += OnInteractCancelled;
        inputContext.FindAction("Shift").performed += OnShiftPerformed;
        inputContext.FindAction("Shift").canceled += OnShiftCancelled;
        inputContext.FindAction("MiddleMouseClick").performed += OnMiddleMousePerformed;
        inputContext.FindAction("MiddleMouseClick").canceled += OnMiddleMouseCancelled;
        inputContext.FindAction("MouseScroll").performed += OnMouseScrollAction;
        inputContext.FindAction("MouseScroll").canceled += OnMouseScrollAction;

        initialized = true;
    }

    protected override void DisableInput(InputActionAsset inputContext)
    {
        initialized = false;

        inputContext.FindAction("Move").performed -= OnMove;
        inputContext.FindAction("Move").canceled -= OnMove;
        inputContext.FindAction("Space").performed -= OnSpacePerformed;
        inputContext.FindAction("Space").canceled -= OnSpaceCancelled;
        inputContext.FindAction("Interact").performed -= OnInteractPerformed;
        inputContext.FindAction("Interact").canceled -= OnInteractCancelled;
        inputContext.FindAction("Shift").performed -= OnShiftPerformed;
        inputContext.FindAction("Shift").canceled -= OnShiftCancelled;
        inputContext.FindAction("MiddleMouseClick").performed -= OnMiddleMousePerformed;
        inputContext.FindAction("MiddleMouseClick").canceled -= OnMiddleMouseCancelled;
        inputContext.FindAction("MouseScroll").performed -= OnMouseScrollAction;
        inputContext.FindAction("MouseScroll").canceled -= OnMouseScrollAction;

        base.DisableInput(inputContext);
    }

    protected virtual void OnMouseScrollAction(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        OnMouseScroll?.Invoke(value);
    }

    protected virtual void OnMiddleMousePerformed(InputAction.CallbackContext context)
    {
        OnMiddleMouse?.Invoke(true);
    }

    protected virtual void OnMiddleMouseCancelled(InputAction.CallbackContext context)
    {
        OnMiddleMouse?.Invoke(false);
    }

    protected virtual void OnShiftPerformed(InputAction.CallbackContext context)
    {
        OnShift?.Invoke(true);
    }

    protected virtual void OnShiftCancelled(InputAction.CallbackContext context)
    {
        OnShift?.Invoke(false);
    }

    protected virtual void OnSpacePerformed(InputAction.CallbackContext context)
    {
        OnSpace?.Invoke(true);
    }

    protected virtual void OnSpaceCancelled(InputAction.CallbackContext context)
    {
        OnSpace?.Invoke(false);
    }

    protected virtual void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        OnMovementInput?.Invoke(input);
    }

    protected virtual void OnInteractPerformed(InputAction.CallbackContext context)
    {
        OnInteract?.Invoke(true);
    }

    protected virtual void OnInteractCancelled(InputAction.CallbackContext context)
    {
        OnInteract?.Invoke(false);
    }
}