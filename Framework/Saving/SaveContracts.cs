using System;
using System.Threading.Tasks;

namespace UnscriptedEngine.Saving
{
    /// <summary>Owns one independent section of a save. Implementations run on the calling Unity thread.</summary>
    public interface ISaveModule
    {
        string Id { get; }
        int Version { get; }
        string Capture(ISaveSerializer serializer);
        // Decode, migrate and validate here without changing gameplay. Null means no saved record.
        // Return an action that applies the prepared state after all modules have prepared successfully.
        Action PrepareRestore(string payload, int version, ISaveSerializer serializer);
    }

    public interface ISaveSerializer
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string value);
    }

    /// <summary>Writes must preserve the other generation. Return null for a missing generation.</summary>
    public interface ISaveStorage
    {
        Task<byte[]> ReadAsync(string slot, int generation);
        Task WriteAsync(string slot, int generation, byte[] data);
        Task DeleteAsync(string slot);
    }

    public enum SaveStatus { Success, NotFound, Busy, Failed }

    public sealed class SaveResult
    {
        private readonly SaveStatus status;
        private readonly Exception error;
        private readonly bool recovered;
        public SaveStatus Status => status;
        public Exception Error => error;
        public bool RecoveredFromCorruption => recovered;
        public bool Succeeded => status == SaveStatus.Success;

        internal SaveResult(SaveStatus status, Exception error = null, bool recovered = false)
        { this.status = status; this.error = error; this.recovered = recovered; }
    }
}
