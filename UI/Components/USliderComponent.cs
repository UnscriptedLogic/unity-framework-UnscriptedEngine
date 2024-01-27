using UnityEngine.Events;
using UnityEngine.UI;
using UnscriptedEngine;

public class USliderComponent : UUIComponent
{
    private Slider slider;

    public Slider Slider => slider;

    public override void InitializeUIComponent(UCanvasController context)
    {
        base.InitializeUIComponent(context);
    
        slider = GetComponent<Slider>();
    }

    public override void OnBindedValueChanged<T>(T value)
    {
        if (float.TryParse(value.ToString(), out float result))
        {
            slider.value = result;
        }
    }
}