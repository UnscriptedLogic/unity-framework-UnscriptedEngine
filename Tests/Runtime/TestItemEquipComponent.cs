using System;
using UnityEngine;
using UnscriptedEngine.Item;

namespace UnscriptedEngine.Tests
{
    // Network tests exercise equipment without requiring a game mode/possession setup.
    public sealed class TestItemEquipComponent : ItemEquipComponent
    {
        protected override bool CanTick => IsSpawned;
    }

    [Serializable]
    public sealed class TestReplicatedItemFragment : UItemFragBehaviour
    {
        public override ItemBehaviour CreateBehaviour(ItemBehaviourContext context) =>
            new ToggleBehaviour(context.GetVisualComponent<Light>());

        private sealed class ToggleBehaviour : ItemBehaviour
        {
            private readonly Light light;
            private bool enabled;
            public ToggleBehaviour(Light light) => this.light = light;
            public override bool ReplicatesState => true;
            public override bool HandlesAction(string action) => action == "primary";
            public override void Equip() => light.enabled = false;
            public override bool Use(string action, bool pressed)
            {
                if (!HandlesAction(action)) return false;
                if (pressed) light.enabled = enabled = !enabled;
                return true;
            }
            public override string CaptureState() => enabled ? "1" : "0";
            public override void ApplyState(string state) => light.enabled = enabled = state == "1";
        }
    }
}
