using System;
using System.IO;
using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>Versioned JSON storage; accepts legacy raw JSON settings files.</summary>
    public sealed class JsonSettingsStore
    {
        [Serializable] private sealed class Envelope { public int schemaVersion; public string payload; }
        private readonly string path;
        private readonly int version;
        private readonly Action<string, string> writeFile;
        private readonly Action<string, string> replaceFile;
        public JsonSettingsStore(string path, int version = 1)
            : this(path, version, File.WriteAllText, (temporary, destination) => File.Replace(temporary, destination, null)) { }

        // Internal I/O seam allows deterministic failure tests without filesystem permission tricks.
        internal JsonSettingsStore(string path, int version, Action<string, string> writeFile, Action<string, string> replaceFile)
        {
            this.path = Path.GetFullPath(path);
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            this.version = version;
            this.writeFile = writeFile ?? throw new ArgumentNullException(nameof(writeFile));
            this.replaceFile = replaceFile ?? throw new ArgumentNullException(nameof(replaceFile));
        }
        public void LoadInto(object target, Action validate = null)
        {
            if (!File.Exists(path)) { validate?.Invoke(); return; }
            string json = File.ReadAllText(path);
            var envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope != null && envelope.schemaVersion != 0)
            {
                if (envelope.schemaVersion != version || string.IsNullOrEmpty(envelope.payload))
                    throw new ArgumentException("Unsupported settings schema version or missing payload.");
                json = envelope.payload;
            }
            JsonUtility.FromJsonOverwrite(json, target);
            validate?.Invoke();
        }
        public void Save(object source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                writeFile(temporary, JsonUtility.ToJson(new Envelope
                    { schemaVersion = version, payload = JsonUtility.ToJson(source) }, true));
                if (File.Exists(path)) replaceFile(temporary, path);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
