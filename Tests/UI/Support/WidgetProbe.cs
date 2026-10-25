using System;
using UnscriptedEngine.UI;
namespace UnscriptedEngine.Tests
{
    public sealed class WidgetProbe : UWidgetComponent
    {
        public bool Consume;
        public int Calls;
        public int Messages;
        public override bool HandleInput(PlayerInputEvent input) { Calls++; return Consume; }
        protected override bool HandleMessage<T>(UWidgetComponent source, T message) { Messages++; return Consume; }
    }
}

