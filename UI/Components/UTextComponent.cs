using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnscriptedEngine
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class UTextComponent : UUIComponent
    {
        protected TextMeshProUGUI tmp;

        public TextMeshProUGUI TMP => tmp;

        public override void InitializeUIComponent(UCanvasController context)
        {
            base.InitializeUIComponent(context);

            tmp = GetComponent<TextMeshProUGUI>();
            tmp.font = theme.PrimaryFont;
        }

        public override void OnBindedValueChanged(object value)
        {
            tmp.text = value.ToString();
        }
    }
}