using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnscriptedEngine.Input
{
    /// <summary>Owns a private action asset instance so one controller cannot disable another's input.</summary>
    public sealed class InputActionSession : IDisposable
    {
        private InputActionAsset actions;
        public InputActionSession(InputActionAsset template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            actions = UnityEngine.Object.Instantiate(template);
        }
        public InputActionMap FindMap(string name, bool required = true) => actions.FindActionMap(name, required);
        public void Dispose()
        {
            if (actions == null) return;
            actions.Disable();
            if (Application.isPlaying) UnityEngine.Object.Destroy(actions);
            else UnityEngine.Object.DestroyImmediate(actions);
            actions = null;
        }
    }
    [Serializable]
    public sealed class PlayerActionNames
    {
        public string playerMap = "Player", uiMap = "UI";
        public string move = "Move", look = "Look", sprint = "Sprint", jump = "Jump", interact = "Interact";
        public string primaryUse = "Attack";
        public string escape = "Escape", descend = "Ctrl", inventory = "Tab", aim = "RightClick", throwItem = "Q", console = "Console";
    }
}
