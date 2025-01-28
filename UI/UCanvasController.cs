using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    public class UCanvasController : ULevelObject
    {
        [SerializeField] private UTheme theme;
        [SerializeField] private Dictionary<string, UUIComponent> components;

        public UTheme CanvasTheme => theme;

        /// <summary>
        /// Called when this UI controller has been created.
        /// </summary>
        /// <param name="context"></param>
        public virtual void OnWidgetAttached(ULevelObject context) 
        {
            components = new Dictionary<string, UUIComponent>();

            BroadcastMessage("InitializeUIComponent", this, SendMessageOptions.DontRequireReceiver);
        }
        
        /// <summary>
        /// Called when this object is to be destroyed 
        /// </summary>
        /// <param name="context"></param>
        public virtual void OnWidgetDetached(ULevelObject context)
        {
            DeInitializeAllComponents();
        }

        public void AddUIComponent(UUIComponent uIComponent)
        {
            if (components.ContainsKey(uIComponent.ID)) return;

            components.Add(uIComponent.ID, uIComponent);
        }

        public T GetUIComponent<T>(string id) where T : UUIComponent
        {
            if (!components.ContainsKey(id))
            {
                BroadcastMessage("InitializeUIComponent", this);
            }

            T uIComponent = components[id] as T;

            if (uIComponent == null)
            {
                Debug.LogError($"UIComponent with ID of {id} could not be found.");
            }

            return uIComponent;
        }

        /// <summary>
        /// Binds a UIComponent to a Bindable variable, subscribing to it's events and updating when something changes.
        /// </summary>
        public void BindUI<T>(ref Bindable<T> variable, string id)
        {
            UUIComponent component = GetUIComponent<UUIComponent>(id);

            variable.OnValueChanged += component.OnBindedValueChanged;
            component.OnBindedValueChanged(variable.Value);
        }

        /// <summary>
        /// Binds a UIComponent to a Bindable variable, subscribing to it's events and updating when something changes.
        /// </summary>
        public void BindUI<T>(ref Bindable<T> variable, string id, Func<T, object> defineBindingMethod)
        {
            UUIComponent component = GetUIComponent<UUIComponent>(id);

            variable.OnValueChanged += (value) =>
            {
                component.OnBindedValueChanged(defineBindingMethod(value));
            };

            component.OnBindedValueChanged(defineBindingMethod(variable.Value));
        }

        /// <summary>
        /// Binds a UIComponent to a Bindable variable, subscribing to it's events and updating when something changes.
        /// </summary>
        public void UnBindUI<T>(ref Bindable<T> variable, string id)
        {
            UUIComponent component = GetUIComponent<UUIComponent>(id);

            variable.OnValueChanged -= component.OnBindedValueChanged;
        }

        /// <summary>
        /// A shorthand to binding a method to any UIComponent that can be subscribed to when interacted with.
        /// </summary>
        public void Bind<T>(string id, UnityAction method) where T : UUIComponent, IBindable
        {
            T component = GetUIComponent<T>(id);

            component.Bind(method);
        }

        /// <summary>
        /// A shorthand to binding a method to any UIComponent that can be subscribed to when interacted with.
        /// Bounded method expects to recieve the ID of the UI component.
        /// </summary>
        public void Bind<T>(string id, UnityAction<string> method) where T : UUIComponent, IBindable
        {
            T component = GetUIComponent<T>(id);

            component.Bind(method);
        }

        /// <summary>
        /// A shorthand to binding a method to any UIComponent that can be subscribed to when interacted with.
        /// returns out the component that was bound to.
        /// </summary>
        public void Bind<T>(string id, UnityAction method, out T component) where T : UUIComponent, IBindable
        {
            component = GetUIComponent<T>(id);

            component.Bind(method);
        }

        /// <summary>
        /// A shorthand to binding a method to any UIComponent that can be subscribed to when interacted with.
        /// Takes in a bindable component
        /// </summary>
        public void Bind(IBindable bindableComponent, UnityAction method)
        {
            bindableComponent.Bind(method);
        }

        /// <summary>
        /// A shorthand to unbinding a method from any UIComponent that can be subscribed to when interacted with.
        /// </summary>
        public void UnBind<T>(string id, UnityAction method) where T : UUIComponent, IBindable
        {
            T component = GetUIComponent<T>(id);

            component.UnBind(method);
        }

        /// <summary>
        /// A shorthand to unbinding all methods from any UIComponent that can be subscribed to when interacted with.
        /// </summary>
        public void UnBind<T>(string id) where T : UUIComponent, IBindable
        {
            T component = GetUIComponent<T>(id);

            component.UnBind();
        }

        protected override void OnLevelStopped()
        {
            base.OnLevelStopped();

            UnBindAllComponents();

            DeInitializeAllComponents();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            UnBindAllComponents();

            DeInitializeAllComponents();
        }

        private void UnBindAllComponents()
        {
            if (components == null) return;

            for (int i = 0; i < components.Count; i++)
            {
                UUIComponent component = components.ElementAt(i).Value;

                IBindable bindable = component as IBindable;
                if (bindable != null)
                {
                    bindable.UnBind();
                }
            }
        }

        private void DeInitializeAllComponents()
        {
            if (components == null) return;

            for (int i = 0; i < components.Count; i++)
            {
                components.ElementAt(i).Value.DeInitializeUIComponent();
            }
        }
    }
}