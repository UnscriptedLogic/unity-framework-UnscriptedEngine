using System;
using System.Threading.Tasks;
using UnscriptedEngine.Saving;
using UnityEngine;

namespace UnscriptedEngine.Samples.Saving
{
    /// <summary>Attach to a GameObject; connect the public methods to UI buttons.</summary>
    public sealed class SaveCounterExample : MonoBehaviour
    {
        [SerializeField, Tooltip("Named save slot. Use ASCII letters, digits, underscores and hyphens.")]
        private string slot = "slot-1";
        [SerializeField, Tooltip("Example gameplay value persisted by the counter module.")]
        private int score;
        private ISaveStorage storage;
        private SaveService saves;
        private bool operationRunning;
        public int Score => score;

        private void OnEnable()
        {
            storage = SaveStorage.CreateDefault();
            saves = new SaveService(storage);
            saves.Register(new CounterModule(this));
        }

        public void AddPoint()
        {
            score++;
            saves.MarkDirty();
        }

        public async void Save() { await RunAsync(false); }
        public async void Load() { await RunAsync(true); }

        private async Task RunAsync(bool load)
        {
            if (operationRunning || saves == null) return;
            operationRunning = true;
            try
            {
                SaveResult result = load ? await saves.LoadAsync(slot) : await saves.SaveAsync(slot);
                if (!result.Succeeded) Debug.LogWarning($"Save example: {result.Status}. {result.Error?.Message}");
                else Debug.Log($"Save example: {(load ? "loaded" : "saved")} score {score}.");
            }
            finally { operationRunning = false; }
        }

        private void OnDisable()
        {
            // Own the browser callback host explicitly; no persistent static service between Play sessions.
            (storage as IDisposable)?.Dispose();
            storage = null;
            saves = null;
        }

        [Serializable] private sealed class CounterData { public int score; }

        private sealed class CounterModule : SaveModule<CounterData>
        {
            private readonly SaveCounterExample owner;
            public CounterModule(SaveCounterExample owner) { this.owner = owner; }
            public override string Id => "example.counter";
            protected override CounterData Capture() => new CounterData { score = owner.score };
            protected override CounterData CreateDefault() => new CounterData();
            protected override void Restore(CounterData data) { owner.score = data.score; }
        }
    }
}
