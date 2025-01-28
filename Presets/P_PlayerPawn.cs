using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnscriptedEngine;

public class P_PlayerPawn : ULevelPawn
{
    protected GI_CustomGameInstance gameInstance;
    protected GM_DefaultGameMode defaultGameMode;

    //Unity Components
    protected Rigidbody rb;
    public Rigidbody Rb
    {
        get
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            return rb;
        }
    }

    protected bool initialized;

    [SerializeField] protected List<PlayerBaseComponent> playerComponents;
    public List<PlayerBaseComponent> PlayerComponents => playerComponents;

    private List<PlayerBaseComponent> updateSorted;
    private List<PlayerBaseComponent> moveSorted;

    protected override void OnLevelStarted()
    {
        base.OnLevelStarted();

        defaultGameMode = GameMode.GetGameMode<GM_DefaultGameMode>();
        gameInstance = GameMode.GetGameInstance<GI_CustomGameInstance>();
    }

    public override void OnPossess(UController uController)
    {
        base.OnPossess(uController);

        if (uController is C_PlayerController playerController)
        {
            playerController.OnMovementInput += OnMove;
            playerController.OnSpace += OnSpace;
            playerController.OnInteract += OnInteract;
            playerController.OnShift += OnShift;
            playerController.OnMiddleMouse += OnMiddleMouse;
            playerController.OnMouseScroll += OnMouseScroll;
        }

        updateSorted = playerComponents.OrderByDescending(x => x.InputPriority.UpdatePriority).ToList();
        moveSorted = playerComponents.OrderByDescending(x => x.InputPriority.MovePriority).ToList();

        initialized = true;
    }

    protected void InitializeComponents(P_PlayerPawn playerPawn)
    {
        for (int i = 0; i < playerComponents.Count; i++)
        {
            playerComponents[i].Initialize(this);
        }
    }

    public override void OnUnPossess(UController uController)
    {
        if (uController is C_PlayerController playerController)
        {
            playerController.OnMovementInput -= OnMove;
            playerController.OnSpace -= OnSpace;
            playerController.OnInteract -= OnInteract;
            playerController.OnShift -= OnShift;
            playerController.OnMiddleMouse -= OnMiddleMouse;
            playerController.OnMouseScroll -= OnMouseScroll;
        }

        base.OnUnPossess(uController);
    }

    protected void DeInitializeComponents(P_PlayerPawn playerPawn)
    {
        for (int i = 0; i < playerComponents.Count; i++)
        {
            playerComponents[i].DeInitialize(this);
        }
    }

    protected virtual void Update()
    {
        if (!initialized) return;

        for (int i = 0; i < updateSorted.Count; i++)
        {
            updateSorted[i].UpdateTick(out bool swallowTick);

            if (swallowTick)
            {
                return;
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!initialized) return;

        for (int i = 0; i < playerComponents.Count; i++)
        {
            playerComponents[i].FixedUpdateTick(out bool swallowTick);

            if (swallowTick)
            {
                return;
            }
        }
    }

    public virtual void OnMove(Vector2 input)
    {
        for (int i = 0; i < moveSorted.Count; i++)
        {
            moveSorted[i].OnMove(input, out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public virtual void OnSpace(bool v)
    {
        List<PlayerBaseComponent> spaceSorted = new List<PlayerBaseComponent>();
        spaceSorted = playerComponents.OrderByDescending(x => x.InputPriority.SpacePriority).ToList();

        for (int i = 0; i < spaceSorted.Count; i++)
        {
            spaceSorted[i].OnSpace(v, out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public override void OnDefaultLeftMouseDown()
    {
        List<PlayerBaseComponent> leftMouseSorted = new List<PlayerBaseComponent>();
        leftMouseSorted = playerComponents.OrderByDescending(x => x.InputPriority.LeftMousePriority).ToList();

        for (int i = 0; i < leftMouseSorted.Count; i++)
        {
            leftMouseSorted[i].OnDefaultLeftMouseDown(out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public override void OnDefaultLeftMouseUp()
    {
        List<PlayerBaseComponent> leftMouseSorted = new List<PlayerBaseComponent>();
        leftMouseSorted = playerComponents.OrderByDescending(x => x.InputPriority.LeftMousePriority).ToList();

        for (int i = 0; i < leftMouseSorted.Count; i++)
        {
            leftMouseSorted[i].OnDefaultLeftMouseUp(out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public override void OnDefaultRightMouseDown()
    {
        List<PlayerBaseComponent> rightMouseSorted = new List<PlayerBaseComponent>();
        rightMouseSorted = playerComponents.OrderByDescending(x => x.InputPriority.RightMousePriority).ToList();

        for (int i = 0; i < rightMouseSorted.Count; i++)
        {
            rightMouseSorted[i].OnDefaultRightMouseDown(out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public override void OnDefaultRightMouseUp()
    {
        List<PlayerBaseComponent> rightMouseSorted = new List<PlayerBaseComponent>();
        rightMouseSorted = playerComponents.OrderByDescending(x => x.InputPriority.RightMousePriority).ToList();

        for (int i = 0; i < rightMouseSorted.Count; i++)
        {
            rightMouseSorted[i].OnDefaultRightMouseUp(out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    private void OnMiddleMouse(bool value)
    {
        if (!initialized) return;

        if (value)
        {
            OnDefaultMiddleMouseDown();
        }
        else
        {
            OnDefaultMiddleMouseUp();
        }
    }

    public virtual void OnDefaultMiddleMouseDown()
    {
        List<PlayerBaseComponent> middleMouseSorted = new List<PlayerBaseComponent>();
        middleMouseSorted = playerComponents.OrderByDescending(x => x.InputPriority.MiddleMousePriority).ToList();

        for (int i = 0; i < middleMouseSorted.Count; i++)
        {
            middleMouseSorted[i].OnDefaultMiddleMouseDown(out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public virtual void OnDefaultMiddleMouseUp()
    {
        List<PlayerBaseComponent> middleMouseSorted = new List<PlayerBaseComponent>();
        middleMouseSorted = playerComponents.OrderByDescending(x => x.InputPriority.MiddleMousePriority).ToList();

        for (int i = 0; i < middleMouseSorted.Count; i++)
        {
            middleMouseSorted[i].OnDefaultMiddleMouseUp(out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public virtual void OnMouseScroll(float value)
    {
        List<PlayerBaseComponent> scrollSorted = new List<PlayerBaseComponent>();
        scrollSorted = playerComponents.OrderByDescending(x => x.InputPriority.MouseScrollPriority).ToList();
        for (int i = 0; i < scrollSorted.Count; i++)
        {
            scrollSorted[i].OnMouseScroll(value, out bool swallowInput);
            if (swallowInput)
            {
                return;
            }
        }
    }

    public virtual void OnInteract(bool v)
    {
        List<PlayerBaseComponent> interactSorted = new List<PlayerBaseComponent>();
        interactSorted = playerComponents.OrderByDescending(x => x.InputPriority.InteractPriority).ToList();

        for (int i = 0; i < interactSorted.Count; i++)
        {
            interactSorted[i].OnInteract(v, out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public virtual void OnShift(bool v)
    {
        List<PlayerBaseComponent> shiftSorted = new List<PlayerBaseComponent>();
        shiftSorted = playerComponents.OrderByDescending(x => x.InputPriority.ShiftPriority).ToList();

        for (int i = 0; i < shiftSorted.Count; i++)
        {
            shiftSorted[i].OnShift(v, out bool swallowInput);

            if (swallowInput)
            {
                return;
            }
        }
    }

    public Vector2 GetDefaultMouseDelta() => GetDefaultInputMap().FindAction("MouseDelta").ReadValue<Vector2>();
    public RaycastHit GetMouseWorldPoint()
    {
        RaycastAtMousePosition(Camera.main, out RaycastHit hit);
        return hit;
    }

    public T GetPlayerComponent<T>() where T : PlayerBaseComponent
    {
        for (int i = 0; i < playerComponents.Count; i++)
        {
            if (playerComponents[i] is T)
            {
                return playerComponents[i] as T;
            }
        }

        return null;
    }

    public T AddPlayerComponent<T>() where T : PlayerBaseComponent
    {
        T newComponent = gameObject.AddComponent<T>();
        playerComponents.Add(newComponent);
        newComponent.Initialize(this);

        return newComponent;
    }

    public void RemovePlayerComponent<T>() where T : PlayerBaseComponent
    {
        for (int i = 0; i < playerComponents.Count; i++)
        {
            if (playerComponents[i] is T)
            {
                playerComponents[i].DeInitialize(this);
                Destroy(playerComponents[i]);
                playerComponents.RemoveAt(i);
                return;
            }
        }
    }
}