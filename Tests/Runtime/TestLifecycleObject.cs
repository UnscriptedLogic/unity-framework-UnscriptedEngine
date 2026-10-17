namespace UnscriptedEngine.Tests
{
    public class TestLifecycleObject : UObject
    {
        public readonly System.Collections.Generic.List<string> FrameTrace = new();
        public readonly System.Collections.Generic.List<string> FixedTrace = new();
        public readonly System.Collections.Generic.List<int> TickFrames = new();
        public readonly System.Collections.Generic.List<float> FixedSteps = new();
        public int SharedTicks { get; private set; }
        public int ServerTicks { get; private set; }
        public int ClientTicks { get; private set; }
        public int SharedFixedTicks { get; private set; }
        public int ServerFixedTicks { get; private set; }
        public int ClientFixedTicks { get; private set; }
        public bool ForceTick { get; set; }
        public bool DisableOnNextTick { get; set; }
        protected override bool CanTick => ForceTick || base.CanTick;
        protected override void Tick(float deltaTime)
        {
            SharedTicks++; FrameTrace.Add("shared"); TickFrames.Add(UnityEngine.Time.frameCount);
            if (!DisableOnNextTick) return;
            DisableOnNextTick = false;
            enabled = false;
        }
        protected override void ServerTick(float deltaTime) { ServerTicks++; FrameTrace.Add("server"); }
        protected override void ClientTick(float deltaTime) { ClientTicks++; FrameTrace.Add("client"); }
        protected override void FixedTick(float deltaTime) { SharedFixedTicks++; FixedTrace.Add("shared"); FixedSteps.Add(UnityEngine.Time.fixedTime); }
        protected override void ServerFixedTick(float deltaTime) { ServerFixedTicks++; FixedTrace.Add("server"); }
        protected override void ClientFixedTick(float deltaTime) { ClientFixedTicks++; FixedTrace.Add("client"); }
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        protected override void BeginPlay() => Begins++;
        protected override void EndPlay() => Ends++;
    }
}
