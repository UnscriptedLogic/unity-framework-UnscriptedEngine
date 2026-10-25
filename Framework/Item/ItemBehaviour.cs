namespace UnscriptedEngine.Item
{
    /// <summary>One equipped session. Store persistent item state outside this object.</summary>
    public abstract class ItemBehaviour : System.IDisposable
    {
        public virtual void Equip() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float deltaTime) { }
        /// <summary>Return true when this behaviour handles the action.</summary>
        public virtual bool Use(string action, bool pressed) => false;
        /// <summary>Opt in to server-only Use and replicated session state.</summary>
        public virtual bool ReplicatesState => false;
        /// <summary>Side-effect-free input consumption check for replicated behaviours.</summary>
        public virtual bool HandlesAction(string action) => false;
        /// <summary>Server snapshot, at most 4093 UTF-8 bytes. Must not mutate gameplay.</summary>
        public virtual string CaptureState() => string.Empty;
        /// <summary>Apply server state to client presentation, including initial/late-join state.</summary>
        public virtual void ApplyState(string state) { }
        /// <summary>Cancel held input/charging without committing an action.</summary>
        public virtual void CancelUse() { }
        public virtual void Unequip() { }
        public virtual void Dispose() { }
    }
}
