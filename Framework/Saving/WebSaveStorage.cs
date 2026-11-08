using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace UnscriptedEngine.Saving
{
    /// <summary>Direct IndexedDB adapter. One operation at a time; dispose on owner shutdown.</summary>
    public sealed class WebSaveStorage : MonoBehaviour, ISaveStorage, IDisposable
    {
        private string database;
        private TaskCompletionSource<string> pending = null;
        private int request = 0;
        private bool disposed;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void UnscriptedSave_Request(string receiver, string database, int request,
            string operation, string slot, int generation, string payload);
#endif

        public static WebSaveStorage Create(string database)
        {
            if (string.IsNullOrWhiteSpace(database)) throw new ArgumentException("Database name is required.");
            GameObject host = new GameObject("SaveStorage-" + Guid.NewGuid().ToString("N"));
            DontDestroyOnLoad(host);
            WebSaveStorage storage = host.AddComponent<WebSaveStorage>();
            storage.database = database;
            return storage;
        }

        public async Task<byte[]> ReadAsync(string slot, int generation)
        {
            string value = await ExecuteAsync("read", slot, generation, "");
            return value == "missing" ? null : Convert.FromBase64String(value);
        }

        public async Task WriteAsync(string slot, int generation, byte[] data)
        { await ExecuteAsync("write", slot, generation, Convert.ToBase64String(data)); }

        public async Task DeleteAsync(string slot)
        { await ExecuteAsync("delete", slot, 0, ""); }

        private async Task<string> ExecuteAsync(string operation, string slot, int generation, string payload)
        {
            SaveService.ValidateSlot(slot);
            if (generation != 0 && generation != 1) throw new ArgumentOutOfRangeException(nameof(generation));
            if (disposed) throw new ObjectDisposedException(nameof(WebSaveStorage));
            if (pending != null) throw new InvalidOperationException("Browser storage is busy.");
#if UNITY_WEBGL && !UNITY_EDITOR
            TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
            pending = completion;
            int currentRequest = ++request;
            try
            {
                UnscriptedSave_Request(gameObject.name, database, currentRequest, operation, slot, generation, payload);
                return await completion.Task;
            }
            finally { pending = null; }
#else
            await Task.CompletedTask;
            throw new PlatformNotSupportedException("WebSaveStorage requires a Web build. Use FileSaveStorage in the Editor.");
#endif
        }

        // Called by the .jslib bridge after the IndexedDB transaction completes or fails.
        [UnityEngine.Scripting.Preserve]
        public void OnStorageComplete(string message)
        {
            string[] parts = message.Split(new[] { '|' }, 3);
            if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                || id != request || pending == null) return;
            if (parts[1] == "ok") pending.TrySetResult(parts[2]);
            else pending.TrySetException(new InvalidOperationException("IndexedDB: " + parts[2]));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pending?.TrySetException(new ObjectDisposedException(nameof(WebSaveStorage)));
            if (this != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            disposed = true;
            pending?.TrySetException(new ObjectDisposedException(nameof(WebSaveStorage)));
        }
    }
}
