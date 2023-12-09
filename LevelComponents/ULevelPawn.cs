using System;
using UnityEngine.InputSystem;

namespace UnscriptedEngine
{
    public abstract class ULevelPawn : ULevelObject
    {
        public static event EventHandler OnPawnCreated;
        public static event EventHandler OnPawnToBeDestroyed;

        protected override void OnLevelStarted()
        {
            base.OnLevelStarted();

            EnableInput(GameMode.InputContext);
        }

        protected override void OnLevelStopped()
        {
            base.OnLevelStopped();

            DisableInput(GameMode.InputContext);
        }

        internal override void FireObjectCreationEvent() => OnPawnCreated?.Invoke(this, EventArgs.Empty);
        internal override void FireObjectDestroyedEvent() => OnPawnToBeDestroyed?.Invoke(this, EventArgs.Empty);

        protected virtual void EnableInput(InputActionAsset inputContext) { }
        protected virtual void DisableInput(InputActionAsset inputContext) { }
    }

}