using System;
using UnscriptedEngine.UI;
namespace UnscriptedEngine.Tests
{
    public sealed class CanvasTestWidget : UCanvasComponent
    {
        public CanvasLayer TestLayer = CanvasLayer.Screen;
        public CanvasInputPolicy Policy = CanvasInputPolicy.Modal;
        public Action<CanvasTestWidget> OnInput;
        public int Calls;
        public bool Initialized;
        public bool ShownAfterInitialization;
        public override CanvasLayer Layer => TestLayer;
        public override CanvasInputPolicy InputPolicy => Policy;
        protected override void OnShown() => ShownAfterInitialization = Initialized && Controller != null;
        public override bool HandleInput(PlayerInputEvent input)
        {
            Calls++;
            OnInput?.Invoke(this);
            return false;
        }
    }

}

