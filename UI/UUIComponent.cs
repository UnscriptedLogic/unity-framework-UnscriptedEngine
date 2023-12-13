using UnityEngine;
using UnityEngine.Events;

namespace UnscriptedEngine
{
    /// <summary>
    /// Dictates whether or not a UIComponent has a bindable property.
    /// </summary>
    public interface IBindable
    {
        void Bind(UnityAction method);

        void Bind(UnityAction<string> method);

        void UnBind(UnityAction method);

        void UnBind();
    }

    public abstract class UUIComponent : MonoBehaviour
    {
        [SerializeField] private string id;
        protected UCanvasController context;
        protected UTheme theme;

        public string ID => id;

        public virtual void InitializeUIComponent(UCanvasController context)
        {
            this.context = context;

            if (id != "")
            {
                context.AddUIComponent(this);
            }

            if (context.CanvasTheme != null)
            {
                theme = context.CanvasTheme;
            }
        }

        public virtual void OnBindedValueChanged<T>(T value) 
        {
            Debug.Log(value);
        }

        public void SetID(string id) => this.id = id;

        public virtual void DeInitializeUIComponent()
        {
            context = null;
        }
    }
}