using UnityEngine;
namespace UnscriptedEngine.Samples
{
    // Assign any lever/valve and a gauge; their appearance remains prefab-authored.
    public class ControlLinkExample : MonoBehaviour
    {
        [SerializeField] private NormalizedValueControl input;
        [SerializeField] private GaugeDisplay output;
        private void OnEnable()
        {
            if (input == null || output == null) return;
            input.OnValueChanged.AddListener(output.SetValue);
            output.SetValue(input.OutputValue);
        }
        private void OnDisable()
        {
            if (input != null && output != null) input.OnValueChanged.RemoveListener(output.SetValue);
        }
    }
}
