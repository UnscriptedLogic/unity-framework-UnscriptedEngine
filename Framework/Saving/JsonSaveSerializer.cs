using System;
using UnityEngine;

namespace UnscriptedEngine.Saving
{
    /// <summary>Uses Unity serialization rules: fields, serializable classes, no dictionaries.</summary>
    public sealed class JsonSaveSerializer : ISaveSerializer
    {
        public string Serialize<T>(T value) => JsonUtility.ToJson(value);
        public T Deserialize<T>(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Save JSON is empty.");
            T result = JsonUtility.FromJson<T>(value);
            if (result == null) throw new ArgumentException("Save JSON has no value.");
            return result;
        }
    }

    /// <summary>Typed module helper. Use data classes containing fields supported by the serializer.</summary>
    public abstract class SaveModule<T> : ISaveModule where T : class
    {
        public abstract string Id { get; }
        public virtual int Version => 1;
        protected abstract T Capture();
        protected abstract T CreateDefault();
        protected abstract void Restore(T data);
        protected virtual void Validate(T data) { }
        protected virtual T Decode(string payload, int version, ISaveSerializer serializer)
        {
            if (version != Version) throw new ArgumentException($"Unsupported {Id} version {version}.");
            return serializer.Deserialize<T>(payload);
        }

        public string Capture(ISaveSerializer serializer)
        {
            T data = Capture();
            if (data == null) throw new InvalidOperationException($"Module {Id} captured null.");
            Validate(data);
            return serializer.Serialize(data);
        }

        public Action PrepareRestore(string payload, int version, ISaveSerializer serializer)
        {
            T data = payload == null ? CreateDefault() : Decode(payload, version, serializer);
            if (data == null) throw new InvalidOperationException($"Module {Id} prepared null.");
            Validate(data);
            return () => Restore(data);
        }
    }
}
