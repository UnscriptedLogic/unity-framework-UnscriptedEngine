using System;
using UnityEngine;
using UnityEngine.Events;
namespace UnscriptedEngine
{
    public abstract class NormalizedValueControl : MonoBehaviour
    {
        [Serializable] public class ValueChangedEvent : UnityEvent<float> { }
        [SerializeField] private Vector2 outputRemap = new(0, 1);
        [SerializeField, Range(0, 1)] private float value;
        [SerializeField] private UInteractableComponent buttonComponent;
        public ValueChangedEvent OnValueChanged = new();
        public UnityEvent OnOpened = new();
        public UnityEvent OnClosed = new();
        public float Value => value;
        public float OutputValue => Mathf.Lerp(outputRemap.x, outputRemap.y, value);
        private int lastExtreme;
        private bool validating;
        private ValueAnimation valueAnimation;
        protected virtual void Awake()
        {
            value = Clamp(value);
            lastExtreme = Extreme;
            ApplyVisual(false);
        }
        protected virtual void OnEnable()
        {
            ApplyVisual(false);
            if (buttonComponent != null) buttonComponent.OnButtonPressedClient += Toggle;
        }
        protected virtual void OnDisable()
        {
            if (buttonComponent != null) buttonComponent.OnButtonPressedClient -= Toggle;
            valueAnimation?.Stop();
        }
        protected virtual void OnDestroy() => valueAnimation?.Stop();
        private void Toggle(UInteractableComponent.UInteractArgs args) => SetValue(value < 0.5f ? 1 : 0);
        public void SetValue(float next)
        {
            next = Clamp(next);
            if (Mathf.Approximately(value, next)) return;
            value = next;
            OnValueChanged.Invoke(OutputValue);
            ApplyVisual(true);
        }
        public void SetInverseValue(float next) => SetValue(1 - next);
        protected abstract void ApplyVisual(bool animate);
        protected void Animate(float seconds, Action<float> apply)
        {
            valueAnimation ??= new ValueAnimation(this);
            valueAnimation.Play(seconds, apply, NotifyExtreme);
        }
        private int Extreme => Mathf.Approximately(value, 0) ? -1 : Mathf.Approximately(value, 1) ? 1 : 0;
        protected void NotifyExtreme()
        {
            int current = Extreme;
            if (validating) { lastExtreme = current; return; }
            if (current == lastExtreme) return;
            lastExtreme = current;
            if (current == -1) OnClosed.Invoke();
            else if (current == 1) OnOpened.Invoke();
        }
        protected virtual void OnValidate()
        {
            value = Clamp(value);
            if (!Application.isPlaying)
            {
                validating = true;
                try { ApplyVisual(false); }
                finally { validating = false; }
            }
        }
        private static float Clamp(float number) => float.IsNaN(number) ? 0 : Mathf.Clamp01(number);
    }
}
