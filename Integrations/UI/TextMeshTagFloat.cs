using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace UnscriptedEngine.UI
{
    /// <summary>Displays a tagged float using local and replicated value-change events.</summary>
    [AddComponentMenu("Unscripted Engine/UI/Text Mesh Tag Float")]
    public sealed class TextMeshTagFloat : MonoBehaviour
    {
        [SerializeField, Tooltip("Value to display. Local changes and received network updates refresh the text.")]
        private TaggedFloatValue source;

        [SerializeField, Tooltip("TextMeshPro label to update. Supports both UI and world-space text.")]
        private TMP_Text targetText;

        [SerializeField, Tooltip("Composite format using {0} for the value. Examples: Health: {0:0} or Stamina: {0:0.0}. Use {{ and }} for literal braces.")]
        private string format = "{0:0}";

        private TaggedFloatValue subscribedSource;
        private bool formatWarningLogged;

        public TaggedFloatValue Source
        {
            get => source;
            set
            {
                source = value;
                if (isActiveAndEnabled) BindSource();
            }
        }

        public TMP_Text TargetText
        {
            get => targetText;
            set { targetText = value; Refresh(); }
        }

        public string Format
        {
            get => format;
            set { format = value; formatWarningLogged = false; Refresh(); }
        }

        private void Reset()
        {
            targetText = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            BindSource();
        }

        private void OnDisable()
        {
            UnbindSource();
        }

        private void BindSource()
        {
            UnbindSource();
            subscribedSource = source;
            if (subscribedSource != null) subscribedSource.OnValueChanged.AddListener(DisplayValue);
            Refresh();
        }

        private void UnbindSource()
        {
            if (subscribedSource != null) subscribedSource.OnValueChanged.RemoveListener(DisplayValue);
            subscribedSource = null;
        }

        /// <summary>Refreshes from the current source; also usable from an Inspector event.</summary>
        public void Refresh()
        {
            if (source != null) DisplayValue(source.Value);
        }

        /// <summary>Displays an event's float argument using the configured format.</summary>
        public void DisplayValue(float value)
        {
            if (targetText == null) return;

            try
            {
                targetText.text = string.Format(CultureInfo.CurrentCulture, format ?? "{0}", value);
            }
            catch (FormatException)
            {
                targetText.text = value.ToString(CultureInfo.CurrentCulture);
                if (formatWarningLogged) return;
                Debug.LogWarning("TextMeshTagFloat has an invalid format. Use {0} for the value, for example Health: {0:0}.", this);
                formatWarningLogged = true;
            }
        }
    }
}
