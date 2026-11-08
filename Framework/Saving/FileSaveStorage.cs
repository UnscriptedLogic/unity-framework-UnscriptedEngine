using System;
using System.IO;
using System.Threading.Tasks;

namespace UnscriptedEngine.Saving
{
    /// <summary>Two independent files per slot; the service alternates generations.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string directory;
        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is required.");
            this.directory = Path.GetFullPath(directory);
        }

        public Task<byte[]> ReadAsync(string slot, int generation)
        {
            string path = GetPath(slot, generation);
            return Task.FromResult(File.Exists(path) ? File.ReadAllBytes(path) : null);
        }

        public Task WriteAsync(string slot, int generation, byte[] data)
        {
            string path = GetPath(slot, generation);
            Directory.CreateDirectory(directory);
            // A failed/truncated write can damage only the older generation.
            File.WriteAllBytes(path, data);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string slot)
        {
            File.Delete(GetPath(slot, 0));
            File.Delete(GetPath(slot, 1));
            return Task.CompletedTask;
        }

        private string GetPath(string slot, int generation)
        {
            SaveService.ValidateSlot(slot);
            if (generation != 0 && generation != 1) throw new ArgumentOutOfRangeException(nameof(generation));
            return Path.Combine(directory, slot + "." + generation + ".save");
        }
    }
}
