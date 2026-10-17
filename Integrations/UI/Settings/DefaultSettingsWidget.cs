using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI.Settings
{
    public sealed class DefaultSettingsWidget : SettingsWidget
    {
        private Toggle toggle;
        private Slider slider;
        private InputField input;
        private Dropdown dropdown;
        private Button reset;
        private Text label;

        protected override void OnBound()
        {
            var rect = (RectTransform)transform;
            SettingsTemplateUI.Vertical(rect);
            SettingsTemplateUI.Background(rect, new Color(.1f, .12f, .15f));
            label = SettingsTemplateUI.Label(rect, Definition.DisplayName);
            if (!string.IsNullOrEmpty(Definition.Description))
            {
                var description = SettingsTemplateUI.Label(rect, Definition.Description, 13);
                description.gameObject.GetComponent<LayoutElement>().preferredHeight = -1;
            }
            bool writable = Service.CanWrite(Definition);
            switch (SettingsTheme.KindOf(Definition))
            {
                case SettingWidgetKind.Boolean:
                    toggle = SettingsTemplateUI.Toggle(rect, "Enabled"); toggle.interactable = writable;
                    toggle.onValueChanged.AddListener(ChangeBool);
                    break;
                case SettingWidgetKind.Float:
                case SettingWidgetKind.Integer:
                    slider = SettingsTemplateUI.Slider(rect);
                    if (Definition is FloatSettingDefinition number) { slider.minValue = number.Minimum; slider.maxValue = number.Maximum; }
                    else if (Definition is IntSettingDefinition integer)
                    { slider.minValue = integer.Minimum; slider.maxValue = integer.Maximum; slider.wholeNumbers = true; }
                    slider.interactable = writable; slider.onValueChanged.AddListener(ChangeNumber);
                    input = SettingsTemplateUI.Input(rect); input.interactable = writable;
                    input.onEndEdit.AddListener(ChangeText);
                    break;
                case SettingWidgetKind.Text:
                    input = SettingsTemplateUI.Input(rect); input.interactable = writable;
                    input.characterLimit = ((StringSettingDefinition)Definition).MaxLength;
                    input.onEndEdit.AddListener(ChangeText);
                    break;
                case SettingWidgetKind.Choice:
                    dropdown = SettingsTemplateUI.Dropdown(rect); dropdown.interactable = writable;
                    dropdown.AddOptions(((StringSettingDefinition)Definition).Choices.ToList());
                    dropdown.onValueChanged.AddListener(ChangeChoice);
                    break;
                default:
                    SettingsTemplateUI.Label(rect, "No editor registered for this setting.", 13);
                    break;
            }
            if (!writable) SettingsTemplateUI.Label(rect, "Controlled by server", 13);
            reset = SettingsTemplateUI.Button(rect, "Reset to default"); reset.interactable = writable;
            reset.onClick.AddListener(ResetValue);
        }
        protected override void DisplayValue(object value)
        {
            if (toggle != null) toggle.SetIsOnWithoutNotify((bool)value);
            if (slider != null) slider.SetValueWithoutNotify(System.Convert.ToSingle(value, CultureInfo.InvariantCulture));
            if (input != null) input.SetTextWithoutNotify(System.Convert.ToString(value, CultureInfo.InvariantCulture));
            if (dropdown != null)
            {
                dropdown.SetValueWithoutNotify(((StringSettingDefinition)Definition).Choices.ToList().IndexOf((string)value));
                dropdown.RefreshShownValue();
            }
        }
        private void ChangeBool(bool value) => Write(value);
        private void ChangeNumber(float value)
        {
            if (Definition is IntSettingDefinition) Write(Mathf.RoundToInt(value));
            else Write(value);
        }
        private void ChangeText(string value)
        {
            if (Definition is StringSettingDefinition) Write(value);
            else if (Definition is IntSettingDefinition && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) Write(integer);
            else if (Definition is FloatSettingDefinition && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) Write(number);
            DisplayValue(Service.Get(Definition));
        }
        private void ChangeChoice(int index) => Write(((StringSettingDefinition)Definition).Choices[index]);
        private void ResetValue() { Service.Reset(Definition); DisplayValue(Service.Get(Definition)); }
        protected override void OnUnbound()
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(ChangeBool);
            if (slider != null) slider.onValueChanged.RemoveListener(ChangeNumber);
            if (input != null) input.onEndEdit.RemoveListener(ChangeText);
            if (dropdown != null) dropdown.onValueChanged.RemoveListener(ChangeChoice);
            if (reset != null) reset.onClick.RemoveListener(ResetValue);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            toggle = null; slider = null; input = null; dropdown = null; reset = null; label = null;
        }
    }
}
