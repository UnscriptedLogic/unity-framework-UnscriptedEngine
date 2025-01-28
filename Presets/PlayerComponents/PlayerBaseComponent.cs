using UnityEngine;

public class PlayerBaseComponent : MonoBehaviour
{
    [System.Serializable]
    public class ComponentInputPriority
    {
        public int LeftMousePriority;
        public int RightMousePriority;
        public int MiddleMousePriority;
        public int MouseScrollPriority;
        public int UpdatePriority;
        public int SpacePriority;
        public int ShiftPriority;
        public int InteractPriority;
        public object MovePriority;
    }

    protected P_PlayerPawn context;
    protected bool initialized;

    [SerializeField] private ComponentInputPriority inputPriority;

    public ComponentInputPriority InputPriority
    {
        get => inputPriority;
        protected set
        {
            inputPriority = value;
        }
    }

    public virtual void Initialize(P_PlayerPawn context)
    {
        this.context = context;

        initialized = true;
    }

    public virtual void ContextEnable() { }
    public virtual void ContextDisable() { }
    public virtual void UpdateTick(out bool swallowTick) { swallowTick = false; }
    public virtual void FixedUpdateTick(out bool swallowTick) { swallowTick = false; }

    public virtual Vector2 GetMousePosition() => context.GetDefaultMousePosition();
    public virtual Vector2 GetMouseDelta() => context.GetDefaultMouseDelta();
    public virtual RaycastHit GetMouseWorldPoint() => context.GetMouseWorldPoint();
    public virtual void OnDefaultLeftMouseDown(out bool swallowInput) { swallowInput = false; }
    public virtual void OnDefaultLeftMouseUp(out bool swallowInput) { swallowInput = false; }
    public virtual void OnDefaultRightMouseDown(out bool swallowInput) { swallowInput = false; }
    public virtual void OnDefaultRightMouseUp(out bool swallowInput) { swallowInput = false; }
    public virtual void OnDefaultMiddleMouseDown(out bool swallowInput) { swallowInput = false; }
    public virtual void OnDefaultMiddleMouseUp(out bool swallowInput) { swallowInput = false; }
    public virtual void OnMouseScroll(float value, out bool swallowInput) { swallowInput = false; }
    public virtual void OnShift(bool pressed, out bool swallowInput) { swallowInput = false; }
    public virtual void OnInteract(bool pressed, out bool swallowInput) { swallowInput = false; }
    public virtual void OnSpace(bool pressed, out bool swallowInput) { swallowInput = false; }
    public virtual void OnMove(Vector2 inputDir, out bool swallowInput) { swallowInput = false; }

    public virtual void DeInitialize(P_PlayerPawn context)
    {
        this.context = null;
    }
}
