using UnityEngine;

namespace Framework.TickSystem
{
    [CreateAssetMenu(fileName = "TickerData", menuName = "Unscripted Engine/Tick System/Ticker Data")]
    public class TickerDataSO : ScriptableObject
    {
        private const float MinTickRate = 0.0001f;

        [Tooltip("Unique ID used by the TickSubSystem registry.")]
        [SerializeField] private string tickID;

        [Tooltip("Ticks emitted per second.")]
        [Min(MinTickRate)]
        [SerializeField] private float tickRate = 1f;

        public string TickID => tickID;
        public float TickRate => tickRate;
        public float TickInterval => tickRate > 0f ? 1f / tickRate : 0f;
        public bool IsValid => !string.IsNullOrWhiteSpace(tickID) && tickRate > 0f;

        private void OnValidate()
        {
            tickID = string.IsNullOrWhiteSpace(tickID) ? string.Empty : tickID.Trim();
            tickRate = Mathf.Max(MinTickRate, tickRate);
        }
    }
}
