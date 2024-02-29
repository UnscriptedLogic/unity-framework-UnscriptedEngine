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

        internal override void FireObjectCreationEvent() => OnPawnCreated?.Invoke(this, EventArgs.Empty);
        internal override void FireObjectDestroyedEvent() => OnPawnToBeDestroyed?.Invoke(this, EventArgs.Empty);

        protected virtual void EnableInput(InputActionAsset inputContext) 
        {
            inputContext.FindAction("MouseClick").performed += DefaultMap_OnMouseDown;
            inputContext.FindAction("MouseRightClick").performed += DefaultMap_OnMouseRightDown;

            inputContext.FindAction("MouseClick").canceled += DefaultMap_OnLeftMouseUp;
            inputContext.FindAction("MouseRightClick").canceled += DefaultMap_OnMouseRightUp;

            inputContext.FindAction("Escape").performed += DefaultMap_OnEscapeDown;
            inputContext.FindAction("Escape").canceled += DefaultMap_OnEscapeUp;
        }
        
        protected virtual void DisableInput(InputActionAsset inputContext)
        {
            inputContext.FindAction("MouseClick").performed -= DefaultMap_OnMouseDown;
            inputContext.FindAction("MouseRightClick").performed -= DefaultMap_OnMouseRightDown;

            inputContext.FindAction("MouseClick").canceled -= DefaultMap_OnLeftMouseUp;
            inputContext.FindAction("MouseRightClick").canceled -= DefaultMap_OnMouseRightUp;

            inputContext.FindAction("Escape").performed -= DefaultMap_OnEscapeDown;
            inputContext.FindAction("Escape").canceled -= DefaultMap_OnEscapeUp;
        }

        #region Raycast Utility

        protected bool RaycastAtMousePosition(Camera camera, out RaycastHit hitinfo, float distance = 100f)
        {
            return Raycast.FromMousePos3D(GetDefaultMousePosition(), camera, out hitinfo, distance);
        }

        protected bool RaycastAtCenter(Camera camera, out RaycastHit hitinfo, float distance = 100f)
        {
            return Raycast.FromCenterCamera(camera, out hitinfo, distance);
        }

        protected bool RaycastAtCenter(Camera camera, LayerMask ignoreLayer, out RaycastHit hitinfo, float distance = 100f)
        {
            return Raycast.FromCenterCamera(camera, ignoreLayer, out hitinfo, distance);
        } 
        #endregion

        #region Default Input Mapping

        public InputActionMap GetDefaultInputMap()
        {
            InputActionMap map = GameMode.InputContext.FindActionMap("Default");

            if (!isUsingDefaultInputMap)
            {
                isUsingDefaultInputMap = true;
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

        public virtual void OnPossess(UController uController) 
        {
            EnableInput(GameMode.InputContext);
        }
            
        public virtual void OnUnPossess(UController uController)
        {
            DisableInput(GameMode.InputContext);
        }

        protected override void OnDestroy()
        {
            if (isUsingDefaultInputMap)
            {
                DisableInput(GameMode.InputContext);
            }

            base.OnDestroy();
        }
    }
}