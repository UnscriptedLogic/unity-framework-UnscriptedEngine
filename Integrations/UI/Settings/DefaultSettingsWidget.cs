using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.UI.Settings
{
    public sealed class DefaultSettingsWidget : SettingsWidget
    {
        private InputField[] vectorInputs;
        private Toggle toggle;
        private Slider slider;
        private InputField input;
        private Dropdown dropdown;
        private Button reset;

        protected override void OnBound()
        {
            RectTransform root = (RectTransform)transform;
            CreateRowHeader(root);
            bool writable = Service.CanWrite(Definition);
            CreateValueEditor(root, writable);
            CreateResetControl(root, writable);
        }

        private void CreateRowHeader(RectTransform root)
        {
            SettingsTemplateUI.Vertical(root);
            SettingsTemplateUI.Background(root, new Color(.1f, .12f, .15f));
            SettingsTemplateUI.Label(root, Definition.DisplayName);
            if (string.IsNullOrEmpty(Definition.Description)) return;
            Text description = SettingsTemplateUI.Label(root, Definition.Description, 13);
            description.GetComponent<LayoutElement>().preferredHeight = -1;
        }

        private void CreateValueEditor(RectTransform root, bool writable)
        {
            switch (SettingsTheme.KindOf(Definition))
            {
                case SettingWidgetKind.Vector2:
                case SettingWidgetKind.Vector3:
                case SettingWidgetKind.Vector4:
                case SettingWidgetKind.Quaternion:
                case SettingWidgetKind.Color:
                    CreateVectorEditor(root, writable);
                    break;
                case SettingWidgetKind.Boolean:
                    toggle = SettingsTemplateUI.Toggle(root, "Enabled");
                    toggle.interactable = writable;
                    toggle.onValueChanged.AddListener(ChangeBoolean);
                    break;
                case SettingWidgetKind.Float:
                case SettingWidgetKind.Integer:
                    CreateNumericEditor(root, writable);
                    break;
                case SettingWidgetKind.Text:
                    CreateTextEditor(root, writable);
                    input.characterLimit = ((StringSettingDefinition)Definition).MaxLength;
                    break;
                case SettingWidgetKind.Choice:
                    dropdown = SettingsTemplateUI.Dropdown(root);
                    dropdown.interactable = writable;
                    dropdown.AddOptions(((StringSettingDefinition)Definition).Choices.ToList());
                    dropdown.onValueChanged.AddListener(ChangeChoice);
                    break;
                default:
                    SettingsTemplateUI.Label(root, "No editor registered for this setting.", 13);
                    break;
            }
        }

        private void CreateVectorEditor(RectTransform root, bool writable)
        {
            int count = Definition is Vector2SettingDefinition ? 2 : Definition is Vector3SettingDefinition ? 3 : 4;
            vectorInputs = new InputField[count];
            string[] labels = Definition is ColorSettingDefinition
                ? new[] { "R", "G", "B", "A" } : new[] { "X", "Y", "Z", "W" };
            for (int index = 0; index < count; index++)
            {
                SettingsTemplateUI.Label(root, labels[index], 13);
                InputField componentInput = SettingsTemplateUI.Input(root);
                componentInput.interactable = writable;
                componentInput.onEndEdit.AddListener(ChangeVector);
                vectorInputs[index] = componentInput;
            }
        }

        private void ChangeVector(string editedText)
        {
            Vector4 value = Vector4.zero;
            for (int index = 0; index < vectorInputs.Length; index++)
            {
                if (!float.TryParse(vectorInputs[index].text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float component))
                {
                    DisplayValue(Service.Get(Definition));
                    return;
                }
                value[index] = component;
            }
            if (Definition is Vector2SettingDefinition) Write(new Vector2(value.x, value.y));
            else if (Definition is Vector3SettingDefinition) Write(new Vector3(value.x, value.y, value.z));
            else if (Definition is QuaternionSettingDefinition) Write(new Quaternion(value.x, value.y, value.z, value.w));
            else if (Definition is ColorSettingDefinition) Write(new Color(value.x, value.y, value.z, value.w));
            else Write(value);
            DisplayValue(Service.Get(Definition));
        }

        private void DisplayVector(object value)
        {
            if (vectorInputs == null) return;
            Vector4 vector;
            if (value is Vector2 vector2) vector = new Vector4(vector2.x, vector2.y, 0f, 0f);
            else if (value is Vector3 vector3) vector = new Vector4(vector3.x, vector3.y, vector3.z, 0f);
            else if (value is Quaternion rotation) vector = new Vector4(rotation.x, rotation.y, rotation.z, rotation.w);
            else if (value is Color color) vector = new Vector4(color.r, color.g, color.b, color.a);
            else vector = (Vector4)value;
            for (int index = 0; index < vectorInputs.Length; index++)
                vectorInputs[index].SetTextWithoutNotify(vector[index].ToString("R", CultureInfo.InvariantCulture));
        }
        private void CreateNumericEditor(RectTransform root, bool writable)
        {
            if (Definition.EditorHint != "number-input") slider = SettingsTemplateUI.Slider(root);
            if (slider != null)
            {
                if (Definition is FloatSettingDefinition number)
                {
                    slider.minValue = number.Minimum;
                    slider.maxValue = number.Maximum;
                }
                else if (Definition is IntSettingDefinition integer)
                {
                    slider.minValue = integer.Minimum;
                    slider.maxValue = integer.Maximum;
                    slider.wholeNumbers = true;
                }
                slider.interactable = writable;
                slider.onValueChanged.AddListener(ChangeNumber);
            }
            CreateTextEditor(root, writable);
        }

        private void CreateTextEditor(RectTransform root, bool writable)
        {
            input = SettingsTemplateUI.Input(root);
            input.interactable = writable;
            input.onEndEdit.AddListener(ChangeText);
        }

        private void CreateResetControl(RectTransform root, bool writable)
        {
            if (!writable) SettingsTemplateUI.Label(root, "Read only", 13);
            reset = SettingsTemplateUI.Button(root, "Reset to default");
            reset.interactable = writable;
            reset.onClick.AddListener(ResetValue);
        }

        protected override void DisplayValue(object value)
        {
            DisplayVector(value);
            if (toggle != null) toggle.SetIsOnWithoutNotify((bool)value);
            if (slider != null) slider.SetValueWithoutNotify(System.Convert.ToSingle(value, CultureInfo.InvariantCulture));
            if (input != null) input.SetTextWithoutNotify(System.Convert.ToString(value, CultureInfo.InvariantCulture));
            if (dropdown == null) return;
            dropdown.SetValueWithoutNotify(((StringSettingDefinition)Definition).Choices.ToList().IndexOf((string)value));
            dropdown.RefreshShownValue();
        }

        private void ChangeBoolean(bool value) => Write(value);

        private void ChangeNumber(float value)
        {
            if (Definition is IntSettingDefinition) Write(Mathf.RoundToInt(value));
            else Write(value);
        }

        private void ChangeText(string value)
        {
            if (Definition is StringSettingDefinition) Write(value);
            else if (Definition is IntSettingDefinition &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer)) Write(integer);
            else if (Definition is FloatSettingDefinition &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) Write(number);
            DisplayValue(Service.Get(Definition));
        }

        private void ChangeChoice(int index) => Write(((StringSettingDefinition)Definition).Choices[index]);

        private void ResetValue()
        {
            Service.Reset(Definition);
            DisplayValue(Service.Get(Definition));
        }

        protected override void OnUnbound()
        {
            RemoveControlListeners();
            DestroyGeneratedControls();
            vectorInputs = null;
            toggle = null;
            slider = null;
            input = null;
            dropdown = null;
            reset = null;
        }

        private void RemoveControlListeners()
        {
            if (vectorInputs != null)
                foreach (InputField componentInput in vectorInputs)
                    componentInput.onEndEdit.RemoveListener(ChangeVector);
            if (toggle != null) toggle.onValueChanged.RemoveListener(ChangeBoolean);
            if (slider != null) slider.onValueChanged.RemoveListener(ChangeNumber);
            if (input != null) input.onEndEdit.RemoveListener(ChangeText);
            if (dropdown != null) dropdown.onValueChanged.RemoveListener(ChangeChoice);
            if (reset != null) reset.onClick.RemoveListener(ResetValue);
        }

        private void DestroyGeneratedControls()
        {
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                GameObject child = transform.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
