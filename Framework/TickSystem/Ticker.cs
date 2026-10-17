using System;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.TickSystem
{
    [DisallowMultipleComponent]
    public class Ticker : MonoBehaviour
    {
        [SerializeField] private TickerDataSO tickerData;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool startTickingOnEnable = true;
        [SerializeField] private UnityEvent onTicked = new();

        private float elapsedTime;
        private bool warnedInvalidData;

        public event Action<Ticker> Ticked;

        public TickerDataSO TickerData => tickerData;
        public string TickID => tickerData != null ? tickerData.TickID : string.Empty;
        public float TickRate => tickerData != null ? tickerData.TickRate : 0f;
        public float TickInterval => tickerData != null ? tickerData.TickInterval : 0f;
        public int TickCount { get; private set; }
        public bool IsTicking { get; private set; }
        public UnityEvent OnTicked => onTicked;

        internal void Initialize(TickerDataSO data, bool startTicking = true)
        {
            tickerData = data;
            warnedInvalidData = false;
            ResetTicker();

            if (tickerData != null && !string.IsNullOrEmpty(tickerData.TickID))
            {
                name = tickerData.TickID;
            }

            if (startTicking)
            {
                StartTicker();
                return;
            }

            StopTicker();
        }

        public void StartTicker()
        {
            if (!HasValidData())
            {
                LogInvalidTickerData();
                StopTicker();
                return;
            }

            IsTicking = true;
            if (isActiveAndEnabled) UnscriptedEngine.UpdateManager.RegisterUpdate(this, ManagedUpdate);
        }

        public void StopTicker(bool resetElapsedTime = false)
        {
            IsTicking = false;
            UnscriptedEngine.UpdateManager.UnregisterUpdate(this);

            if (resetElapsedTime)
            {
                elapsedTime = 0f;
            }
        }

        public void ResetTicker()
        {
            elapsedTime = 0f;
            TickCount = 0;
        }

        private void OnEnable()
        {
            TickSubSystem.RegisterTicker(this);

            if (startTickingOnEnable)
            {
                StartTicker();
            }
            else if (IsTicking)
            {
                UnscriptedEngine.UpdateManager.RegisterUpdate(this, ManagedUpdate);
            }
        }

        private void ManagedUpdate(float deltaTime)
        {
            if (!IsTicking)
            {
                return;
            }

            if (!HasValidData())
            {
                LogInvalidTickerData();
                IsTicking = false;
                UnscriptedEngine.UpdateManager.UnregisterUpdate(this);
                return;
            }

            float tickInterval = TickInterval;
            elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : deltaTime;

            while (this != null && isActiveAndEnabled && IsTicking && elapsedTime >= tickInterval)
            {
                elapsedTime -= tickInterval;
                EmitTick();
            }
        }

        private void OnDisable()
        {
            UnscriptedEngine.UpdateManager.UnregisterUpdate(this);
            TickSubSystem.UnregisterTicker(this);
        }

        private void OnDestroy()
        {
            UnscriptedEngine.UpdateManager.UnregisterUpdate(this);
            TickSubSystem.UnregisterTicker(this);
            Ticked = null;
        }

        private bool HasValidData()
        {
            return tickerData != null && tickerData.IsValid;
        }

        private void EmitTick()
        {
            TickCount++;
            Ticked?.Invoke(this);
            onTicked?.Invoke();
        }

        private void LogInvalidTickerData()
        {
            if (warnedInvalidData)
            {
                return;
            }

            warnedInvalidData = true;
            Debug.LogWarning($"{nameof(Ticker)} on {name} requires a valid {nameof(TickerDataSO)} with a TickID and TickRate greater than zero.", this);
        }

        private void OnValidate()
        {
            if (tickerData != null && !string.IsNullOrEmpty(tickerData.TickID))
            {
                name = tickerData.TickID;
            }
        }
    }
}
