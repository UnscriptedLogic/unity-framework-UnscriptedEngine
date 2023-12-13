using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnscriptedEngine
{
    public abstract class ULevelPawn : ULevelObject
    {
        protected bool isUsingDefaultInputMap = false;

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

        #region Default Input Mapping

        public InputActionMap GetDefaultInputMap()
        {
            InputActionMap map = GameMode.InputContext.FindActionMap("Default");

            if (!isUsingDefaultInputMap)
            {
                isUsingDefaultInputMap = true;

                map.FindAction("MouseClick").performed += DefaultMap_OnMouseDown;
                map.FindAction("MouseRightClick").performed += DefaultMap_OnMouseRightDown;

                map.FindAction("MouseClick").canceled += DefaultMap_OnLeftMouseUp;
                map.FindAction("MouseRightClick").canceled += DefaultMap_OnMouseRightUp;

                map.FindAction("Escape").performed += DefaultMap_OnEscapeDown;
                map.FindAction("Escape").canceled += DefaultMap_OnEscapeUp;
            }

            return map;
        }

        private void DefaultMap_OnMouseDown(InputAction.CallbackContext obj) => OnDefaultLeftMouseDown();
        private void DefaultMap_OnMouseRightDown(InputAction.CallbackContext obj) => OnDefaultRightMouseDown();

        private void DefaultMap_OnLeftMouseUp(InputAction.CallbackContext obj) => OnDefaultLeftMouseUp();
        private void DefaultMap_OnMouseRightUp(InputAction.CallbackContext context) => OnDefaultRightMouseUp();

        private void DefaultMap_OnEscapeUp(InputAction.CallbackContext context) => OnDefaultEscapeUp();
        private void DefaultMap_OnEscapeDown(InputAction.CallbackContext obj) => OnDefaultEscapeDown();

        public Vector2 GetDefaultMousePosition() => GetDefaultInputMap().FindAction("MousePosition").ReadValue<Vector2>();

        public virtual void OnDefaultLeftMouseDown() { }
        public virtual void OnDefaultRightMouseDown() { }

        public virtual void OnDefaultLeftMouseUp() { }
        public virtual void OnDefaultRightMouseUp() { }

        public virtual void OnDefaultEscapeUp() { }
        public virtual void OnDefaultEscapeDown() { }

        #endregion

        protected override void OnDestroy()
        {
            if (isUsingDefaultInputMap)
            {
                GetDefaultInputMap().FindAction("MouseClick").performed -= DefaultMap_OnMouseDown;
                GetDefaultInputMap().FindAction("MouseRightClick").performed -= DefaultMap_OnMouseRightDown;

                GetDefaultInputMap().FindAction("MouseClick").canceled -= DefaultMap_OnLeftMouseUp;
                GetDefaultInputMap().FindAction("MouseRightClick").canceled -= DefaultMap_OnMouseRightUp;

                GetDefaultInputMap().FindAction("Escape").performed -= DefaultMap_OnEscapeDown;
                GetDefaultInputMap().FindAction("Escape").canceled -= DefaultMap_OnEscapeUp;
            }

            base.OnDestroy();
        }
    }
}