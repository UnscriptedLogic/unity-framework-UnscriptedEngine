namespace UnscriptedEngine {
using UnscriptedEngine;
using UnityEngine;
using UnityEngine.Events;

public class GaugeDisplay : MonoBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    [System.Serializable]
    public class ValueChangedEvent : UnityEvent<float>
    {
    }

    [SerializeField] private Transform needle;
    [SerializeField, Min(0.0001f)] private float maxValue = 1f;
    [SerializeField, Min(0f)] private float value;
    [SerializeField] private Vector3 leftLocalEulerAngles;
    [SerializeField] private Vector3 rightLocalEulerAngles;
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private Gradient indicatorColorGradient = CreateDefaultIndicatorColorGradient();
    [SerializeField, Min(0f)] private float animationDuration = 0.25f;

    public ValueChangedEvent OnValueChanged = new();

    public float Value => value;
    public float MaxValue => maxValue;

    private ValueAnimation needleTween;
    private MaterialPropertyBlock indicatorPropertyBlock;

    protected virtual void Awake()
    {
        maxValue = Mathf.Max(0.0001f, maxValue);
        value = Mathf.Clamp(value, 0f, maxValue);
        ApplyValueVisuals();
    }

    protected virtual void OnEnable() => ApplyValueVisuals();
    protected virtual void OnDisable() => needleTween?.Stop();

    protected virtual void OnDestroy()
    {
        needleTween?.Stop();
    }

    /// <summary>
    /// Sets the gauge value. Zero is the left position and max value is the right position.
    /// </summary>
    public void SetValue(float newValue)
    {
        newValue = Mathf.Clamp(newValue, 0f, maxValue);

        if (Mathf.Approximately(value, newValue))
        {
            value = newValue;
            return;
        }

        value = newValue;
        OnValueChanged.Invoke(value);
        ApplyIndicatorColor();
        AnimateNeedle();
    }

    private void AnimateNeedle()
    {
        if (needle == null)
        {
            return;
        }

        needleTween?.Stop();

        Vector3 targetEulerAngles = Vector3.Lerp(leftLocalEulerAngles, rightLocalEulerAngles, GetNormalizedValue());
        if (animationDuration <= 0f)
        {
            needle.localEulerAngles = targetEulerAngles;
            return;
        }

        Quaternion start = needle.localRotation;
        Quaternion target = Quaternion.Euler(targetEulerAngles);
        needleTween ??= new ValueAnimation(this);
        needleTween.Play(animationDuration, t => needle.localRotation = Quaternion.Slerp(start, target, t));
    }

    private void ApplyNeedleRotation()
    {
        if (needle == null)
        {
            return;
        }

        needle.localEulerAngles = Vector3.Lerp(leftLocalEulerAngles, rightLocalEulerAngles, GetNormalizedValue());
    }

    private void ApplyIndicatorColor()
    {
        if (indicatorRenderer == null || indicatorColorGradient == null)
        {
            return;
        }

        indicatorPropertyBlock ??= new MaterialPropertyBlock();
        indicatorRenderer.GetPropertyBlock(indicatorPropertyBlock);

        Color indicatorColor = indicatorColorGradient.Evaluate(GetNormalizedValue());
        indicatorPropertyBlock.SetColor(BaseColorPropertyId, indicatorColor);
        indicatorPropertyBlock.SetColor(ColorPropertyId, indicatorColor);
        indicatorRenderer.SetPropertyBlock(indicatorPropertyBlock);
    }

    private void ApplyValueVisuals()
    {
        ApplyNeedleRotation();
        ApplyIndicatorColor();
    }

    private float GetNormalizedValue()
    {
        return maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);
    }

    private static Gradient CreateDefaultIndicatorColorGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.green, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        return gradient;
    }

    protected virtual void OnValidate()
    {
        maxValue = Mathf.Max(0.0001f, maxValue);
        value = Mathf.Clamp(value, 0f, maxValue);

        if (!Application.isPlaying)
        {
            ApplyValueVisuals();
        }
    }
}

}
