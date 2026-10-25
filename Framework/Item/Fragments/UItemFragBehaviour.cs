using System;
using UnscriptedEngine.Item;

namespace UnscriptedEngine
{
    /// <summary>Shared settings/factory. Return a fresh behaviour for each equipped session.</summary>
    [Serializable]
    public abstract class UItemFragBehaviour : UItemFragment
    {
        public override string ID => "behaviour";
        public abstract ItemBehaviour CreateBehaviour(ItemBehaviourContext context);
    }
}
