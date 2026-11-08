using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace UnscriptedEngine.Saving
{
    /// <summary>Instance-owned, main-thread coordinator. No static state or automatic scene scanning.</summary>
    public sealed class SaveService
    {
        [Serializable] private sealed class Record
        { public string id; public int version; public string payload; }
        [Serializable] private sealed class Document
        { public int format; public List<Record> modules; }
        [Serializable] private sealed class Envelope
        { public long sequence; public string hash; public string payload; }
        private sealed class Snapshot
        { public int Generation; public Envelope Envelope; public Document Document; }

        private readonly ISaveStorage storage;
        private readonly ISaveSerializer serializer;
        private readonly List<ISaveModule> modules = new List<ISaveModule>();
        private bool busy;
        private long revision;
        private long savedRevision;
        public bool IsBusy => busy;
        public bool IsDirty => revision != savedRevision;

        public SaveService(ISaveStorage storage, ISaveSerializer serializer = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.serializer = serializer ?? new JsonSaveSerializer();
        }

        public void Register(ISaveModule module)
        {
            EnsureIdle();
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (string.IsNullOrWhiteSpace(module.Id) || module.Version < 1)
                throw new ArgumentException("Module ID and positive version are required.");
            foreach (ISaveModule existing in modules)
                if (existing.Id == module.Id) throw new ArgumentException($"Duplicate save module: {module.Id}");
            modules.Add(module);
        }

        public bool Unregister(ISaveModule module) { EnsureIdle(); return modules.Remove(module); }
        public void MarkDirty() { unchecked { revision++; } }

        public async Task<SaveResult> SaveAsync(string slot)
        {
            if (busy) return new SaveResult(SaveStatus.Busy);
            busy = true;
            try
            {
                ValidateSlot(slot);
                long capturedRevision = revision;
                Snapshot previous = await ReadLatestAsync(slot);
                Document document = previous == null ? new Document { format = 1, modules = new List<Record>() }
                    : previous.Document;
                foreach (ISaveModule module in modules)
                {
                    Record record = new Record { id = module.Id, version = module.Version, payload = module.Capture(serializer) };
                    if (string.IsNullOrEmpty(record.payload)) throw new InvalidOperationException($"Empty payload: {module.Id}");
                    int index = document.modules.FindIndex(existing => existing.id == module.Id);
                    if (index >= 0 && document.modules[index].version > module.Version)
                        throw new NotSupportedException($"Module {module.Id} was saved by a newer version.");
                    if (index < 0) document.modules.Add(record); else document.modules[index] = record;
                }
                string payload = serializer.Serialize(document);
                Envelope envelope = new Envelope
                {
                    sequence = previous == null ? 1 : checked(previous.Envelope.sequence + 1),
                    payload = payload, hash = Hash(payload)
                };
                int generation = previous == null ? 0 : 1 - previous.Generation;
                await storage.WriteAsync(slot, generation, Encoding.UTF8.GetBytes(serializer.Serialize(envelope)));
                savedRevision = capturedRevision;
                return new SaveResult(SaveStatus.Success);
            }
            catch (Exception exception) { return new SaveResult(SaveStatus.Failed, exception); }
            finally { busy = false; }
        }

        public async Task<SaveResult> LoadAsync(string slot)
        {
            if (busy) return new SaveResult(SaveStatus.Busy);
            busy = true;
            try
            {
                ValidateSlot(slot);
                Snapshot snapshot = await ReadLatestAsync(slot);
                if (snapshot == null) return new SaveResult(SaveStatus.NotFound);
                List<Action> restore = new List<Action>(modules.Count);
                foreach (ISaveModule module in modules)
                {
                    Record record = snapshot.Document.modules.Find(existing => existing.id == module.Id);
                    Action apply = module.PrepareRestore(record?.payload, record?.version ?? 0, serializer);
                    restore.Add(apply ?? throw new InvalidOperationException($"No restore action: {module.Id}"));
                }
                long preparedRevision = revision;
                foreach (Action apply in restore) apply();
                savedRevision = preparedRevision;
                return new SaveResult(SaveStatus.Success, recovered: recoveredRead);
            }
            catch (Exception exception) { return new SaveResult(SaveStatus.Failed, exception); }
            finally { busy = false; }
        }

        public async Task<SaveResult> DeleteAsync(string slot)
        {
            if (busy) return new SaveResult(SaveStatus.Busy);
            busy = true;
            try { ValidateSlot(slot); await storage.DeleteAsync(slot); return new SaveResult(SaveStatus.Success); }
            catch (Exception exception) { return new SaveResult(SaveStatus.Failed, exception); }
            finally { busy = false; }
        }

        private bool recoveredRead;
        private async Task<Snapshot> ReadLatestAsync(string slot)
        {
            Snapshot latest = null;
            Exception invalid = null;
            recoveredRead = false;
            for (int generation = 0; generation < 2; generation++)
            {
                // Storage failures are not corruption; never silently overwrite unreadable storage.
                byte[] bytes = await storage.ReadAsync(slot, generation);
                if (bytes == null) continue;
                try
                {
                    Envelope envelope = serializer.Deserialize<Envelope>(Encoding.UTF8.GetString(bytes));
                    if (envelope.sequence < 1 || string.IsNullOrEmpty(envelope.payload) || envelope.hash != Hash(envelope.payload))
                        throw new FormatException("Save checksum or sequence is invalid.");
                    Document document = serializer.Deserialize<Document>(envelope.payload);
                    if (document.format != 1) throw new NotSupportedException($"Unsupported save format {document.format}.");
                    if (document.modules == null) throw new FormatException("Missing module records.");
                    HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Record record in document.modules)
                        if (record == null || string.IsNullOrWhiteSpace(record.id) || record.version < 1 ||
                            string.IsNullOrEmpty(record.payload) || !ids.Add(record.id))
                            throw new FormatException("Invalid or duplicate module record.");
                    if (latest == null || envelope.sequence > latest.Envelope.sequence)
                        latest = new Snapshot { Generation = generation, Envelope = envelope, Document = document };
                }
                catch (NotSupportedException) { throw; } // Future formats must not be overwritten.
                catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
                { invalid = exception; }
            }
            if (latest == null && invalid != null) throw new FormatException("No valid save generation remains.", invalid);
            recoveredRead = invalid != null;
            return latest;
        }

        private static string Hash(string payload)
        {
            using (SHA256 algorithm = SHA256.Create())
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }

        internal static void ValidateSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length > 64) throw new ArgumentException("Slot must contain 1–64 ASCII letters, digits, underscores or hyphens.");
            foreach (char character in slot)
                if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                    !(character >= '0' && character <= '9') && character != '_' && character != '-')
                    throw new ArgumentException("Invalid slot name.");
        }

        private void EnsureIdle()
        { if (busy) throw new InvalidOperationException("Cannot change modules during save/load/delete."); }
    }
}
