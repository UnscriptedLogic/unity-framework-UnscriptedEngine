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
        /// <summary>Cancel held input/charging without committing an action.</summary>
        public virtual void CancelUse() { }
        public virtual void Unequip() { }
        public virtual void Dispose() { }
    }
}
