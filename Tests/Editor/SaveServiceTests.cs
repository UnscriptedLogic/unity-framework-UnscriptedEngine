using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnscriptedEngine.Saving;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class SaveServiceTests
    {
        [Serializable] private sealed class Data { public int value; }
        private sealed class Module : SaveModule<Data>
        {
            private readonly string id;
            public int Value;
            public bool Reject;
            public Module(string id = "test") { this.id = id; }
            public override string Id => id;
            protected override Data Capture() => new Data { value = Value };
            protected override Data CreateDefault() => new Data { value = 7 };
            protected override void Restore(Data data) { Value = data.value; }
            protected override void Validate(Data data)
            { if (Reject) throw new ArgumentException("Invalid module."); }
        }

        private sealed class MemoryStorage : ISaveStorage
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public bool FailWrite;
            public TaskCompletionSource<bool> Gate;
            public async Task<byte[]> ReadAsync(string slot, int generation)
            {
                if (Gate != null) await Gate.Task;
                return Files.TryGetValue(slot + generation, out byte[] value) ? value : null;
            }
            public Task WriteAsync(string slot, int generation, byte[] data)
            {
                Files[slot + generation] = FailWrite ? Encoding.UTF8.GetBytes("truncated") : data;
                if (FailWrite) throw new IOException("Interrupted write.");
                return Task.CompletedTask;
            }
            public Task DeleteAsync(string slot)
            { Files.Remove(slot + "0"); Files.Remove(slot + "1"); return Task.CompletedTask; }
        }

        [Serializable] private sealed class FixtureEnvelope
        { public long sequence; public string hash; public string payload; }

        private sealed class MigratingModule : SaveModule<Data>
        {
            public int Value;
            public override string Id => "test";
            public override int Version => 2;
            protected override Data Capture() => new Data { value = Value };
            protected override Data CreateDefault() => new Data();
            protected override void Restore(Data data) { Value = data.value; }
            protected override Data Decode(string payload, int version, ISaveSerializer serializer)
            {
                if (version == 1) return new Data { value = serializer.Deserialize<Data>(payload).value + 10 };
                return base.Decode(payload, version, serializer);
            }
        }

        private static byte[] Fixture(string document)
        {
            using (SHA256 algorithm = SHA256.Create())
                return Encoding.UTF8.GetBytes(JsonUtility.ToJson(new FixtureEnvelope
                {
                    sequence = 1, payload = document,
                    hash = Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(document)))
                }));
        }

        [Test] public async Task OldModuleVersionMigratesAndResavesCurrentVersion()
        {
            MemoryStorage storage = new MemoryStorage();
            storage.Files["slot0"] = Fixture("{\"format\":1,\"modules\":[{\"id\":\"test\",\"version\":1,\"payload\":\"{\\\"value\\\":5}\"}]}");
            MigratingModule module = new MigratingModule();
            SaveService service = new SaveService(storage); service.Register(module);
            Assert.That((await service.LoadAsync("slot")).Succeeded, Is.True);
            Assert.That(module.Value, Is.EqualTo(15));
            Assert.That((await service.SaveAsync("slot")).Succeeded, Is.True);
            module.Value = 0;
            Assert.That((await service.LoadAsync("slot")).Succeeded, Is.True);
            Assert.That(module.Value, Is.EqualTo(15), "Current payload must not migrate twice.");
        }

        [Test] public async Task FutureDocumentAndModuleVersionsAreNotOverwritten()
        {
            MemoryStorage storage = new MemoryStorage();
            SaveService service = new SaveService(storage); service.Register(new Module());
            storage.Files["slot0"] = Fixture("{\"format\":99,\"modules\":[]}");
            Assert.That((await service.SaveAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            storage.Files["slot0"] = Fixture("{\"format\":1,\"modules\":[{\"id\":\"test\",\"version\":99,\"payload\":\"{}\"}]}");
            Assert.That((await service.LoadAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That((await service.SaveAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(storage.Files.Count, Is.EqualTo(1));
        }

        [Test] public void DuplicateModulesAreRejected()
        {
            SaveService service = new SaveService(new MemoryStorage());
            service.Register(new Module());
            Assert.Throws<ArgumentException>(() => service.Register(new Module()));
        }

        [Test] public async Task AlternatingGenerationsRecoverFromInterruptedWrite()
        {
            MemoryStorage storage = new MemoryStorage();
            SaveService service = new SaveService(storage);
            Module module = new Module { Value = 10 };
            service.Register(module);
            Assert.That((await service.SaveAsync("slot")).Succeeded, Is.True);
            module.Value = 20;
            Assert.That((await service.SaveAsync("slot")).Succeeded, Is.True);
            module.Value = 30;
            service.MarkDirty();
            storage.FailWrite = true;
            Assert.That((await service.SaveAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(service.IsDirty, Is.True);
            SaveResult result = await service.LoadAsync("slot");
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.RecoveredFromCorruption, Is.True);
            Assert.That(module.Value, Is.EqualTo(20));
        }

        [Test] public async Task UnknownModulesSurviveDifferentServiceAndSlotSwitch()
        {
            MemoryStorage storage = new MemoryStorage();
            SaveService first = new SaveService(storage);
            Module original = new Module("optional") { Value = 42 };
            first.Register(original);
            await first.SaveAsync("one");
            SaveService second = new SaveService(storage);
            second.Register(new Module("other"));
            Assert.That((await second.SaveAsync("one")).Succeeded, Is.True);
            await second.SaveAsync("two");
            original.Value = 0;
            Assert.That((await first.LoadAsync("one")).Succeeded, Is.True);
            Assert.That(original.Value, Is.EqualTo(42));
            Assert.That((await first.LoadAsync("two")).Succeeded, Is.True);
            Assert.That(original.Value, Is.EqualTo(7), "Absent module uses defaults, not previous slot state.");
        }

        [Test] public async Task PreparationFailureDoesNotApplyAnyModule()
        {
            MemoryStorage storage = new MemoryStorage();
            SaveService service = new SaveService(storage);
            Module first = new Module("first") { Value = 1 };
            Module second = new Module("second") { Value = 2 };
            service.Register(first); service.Register(second);
            await service.SaveAsync("slot");
            first.Value = 100; second.Value = 200; second.Reject = true;
            Assert.That((await service.LoadAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(first.Value, Is.EqualTo(100));
            Assert.That(second.Value, Is.EqualTo(200));
        }

        [Test] public async Task BusyOperationsDoNotOverlapAndChangesDuringSaveRemainDirty()
        {
            MemoryStorage storage = new MemoryStorage { Gate = new TaskCompletionSource<bool>() };
            SaveService service = new SaveService(storage);
            service.Register(new Module());
            service.MarkDirty();
            Task<SaveResult> save = service.SaveAsync("slot");
            Assert.That(service.IsBusy, Is.True);
            Assert.That((await service.LoadAsync("slot")).Status, Is.EqualTo(SaveStatus.Busy));
            Assert.Throws<InvalidOperationException>(() => service.Register(new Module("late")));
            service.MarkDirty();
            storage.Gate.SetResult(true);
            Assert.That((await save).Succeeded, Is.True);
            Assert.That(service.IsDirty, Is.True);
            Assert.That((await service.SaveAsync("slot")).Succeeded, Is.True);
            Assert.That(service.IsDirty, Is.False);
        }

        [Test] public async Task CorruptOnlyGenerationIsNotSilentlyOverwritten()
        {
            MemoryStorage storage = new MemoryStorage();
            storage.Files["slot0"] = Encoding.UTF8.GetBytes("{}");
            SaveService service = new SaveService(storage);
            Assert.That((await service.SaveAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(storage.Files.Count, Is.EqualTo(1));
        }

        [Test] public async Task ChecksumDetectsValidJsonPayloadModification()
        {
            MemoryStorage storage = new MemoryStorage();
            SaveService service = new SaveService(storage);
            Module module = new Module { Value = 12345 };
            service.Register(module);
            await service.SaveAsync("slot");
            string tampered = Encoding.UTF8.GetString(storage.Files["slot0"]).Replace("12345", "99999");
            storage.Files["slot0"] = Encoding.UTF8.GetBytes(tampered);
            Assert.That((await service.LoadAsync("slot")).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(module.Value, Is.EqualTo(12345));
        }

        [TestCase("../escape")] [TestCase("a/b")] [TestCase("")]
        public async Task InvalidSlotsFailWithoutWriting(string slot)
        {
            MemoryStorage storage = new MemoryStorage();
            Assert.That((await new SaveService(storage).SaveAsync(slot)).Status, Is.EqualTo(SaveStatus.Failed));
            Assert.That(storage.Files, Is.Empty);
        }

        [Test] public async Task MissingSlotAndDeletionDoNotResetGameplay()
        {
            MemoryStorage storage = new MemoryStorage();
            SaveService service = new SaveService(storage);
            Module module = new Module { Value = 99 };
            service.Register(module);
            Assert.That((await service.LoadAsync("missing")).Status, Is.EqualTo(SaveStatus.NotFound));
            Assert.That(module.Value, Is.EqualTo(99));
            await service.SaveAsync("slot"); await service.SaveAsync("slot");
            Assert.That((await service.DeleteAsync("slot")).Succeeded, Is.True);
            Assert.That(storage.Files, Is.Empty);
        }

        [Test] public async Task FileStoragePersistsAcrossInstances()
        {
            string directory = Path.Combine(Path.GetTempPath(), "UnscriptedSave-" + Guid.NewGuid().ToString("N"));
            try
            {
                SaveService writer = new SaveService(new FileSaveStorage(directory));
                writer.Register(new Module { Value = 31 });
                Assert.That((await writer.SaveAsync("slot")).Succeeded, Is.True);
                SaveService reader = new SaveService(new FileSaveStorage(directory));
                Module module = new Module(); reader.Register(module);
                Assert.That((await reader.LoadAsync("slot")).Succeeded, Is.True);
                Assert.That(module.Value, Is.EqualTo(31));
                await reader.DeleteAsync("slot");
                Assert.That(Directory.GetFiles(directory), Is.Empty);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
