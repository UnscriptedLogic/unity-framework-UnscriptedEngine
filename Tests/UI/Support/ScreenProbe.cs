using System;
using UnscriptedEngine.UI;
namespace UnscriptedEngine.Tests
{
    public sealed class ScreenProbe : UCanvasComponent
    {
        public int Messages;
        public override CanvasInputPolicy InputPolicy => CanvasInputPolicy.Modal;
        protected override bool HandleMessage<T>(UWidgetComponent source, T message) { Messages++; return true; }
    }
}

