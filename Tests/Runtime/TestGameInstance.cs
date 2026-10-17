using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public class TestGameInstance : UGameInstance
    {
        public int InitializeCount { get; private set; }
        public int ShutdownCount { get; private set; }
        protected override void InitializeGameInstance() => InitializeCount++;
        protected override void ShutdownGameInstance() => ShutdownCount++;
        public void SimulateQuit() => OnApplicationQuit();
    }
}
