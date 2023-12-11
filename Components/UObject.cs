using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine;

public class UObject : MonoBehaviour
{
    public T CastTo<T>() where T : UObject
    {
        if (this as T)
        {
            return (T)this;
        }

        return default(T);
    }

    public void CastTo<T>(Action<T> OnSuccess, Action OnFailure = null) where T : UObject
    {
        if (this as T)
        {
            OnSuccess(this as T);
        }
        else
        {
            OnFailure?.Invoke();
        }
    }

    /// <summary>
    /// Defines a bindable variable that can be subscribed to for changes using the OnValueChanged event.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [System.Serializable]
    public class Bindable<T>
    {
        private T _value;
        public T Value
        {
            get { return _value; }
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _value = value;
                    OnValueChanged?.Invoke(_value);
                }
            }
        }

        public Action<T> OnValueChanged;

        public Bindable(T initialValue)
        {
            _value = initialValue;
        }
    }
}
