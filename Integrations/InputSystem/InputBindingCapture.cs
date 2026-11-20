using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnscriptedEngine.Input
{
    /// <summary>Owns the temporary input focus and one interactive binding operation.</summary>
    public sealed class InputBindingCapture : IDisposable
    {
        private static InputBindingCapture active;
        private static int finishedFrame = -1;
        public static bool BlocksInput => active != null || finishedFrame == Time.frameCount;
        public bool IsListening => active == this;

        private InputActionRebindingExtensions.RebindingOperation operation;
        private IDisposable focus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            active?.Dispose();
            finishedFrame = -1;
        }

        public void Start(UnityEngine.Object owner, InputBindingSetting definition, Action<string> finished)
        {
            if (BlocksInput) return;
            active = this;
            try
            {
                focus = InputFocus.Acquire(owner);

                string selectedPath = null;
                operation = new InputActionRebindingExtensions.RebindingOperation()
                    .WithExpectedControlType(definition.ControlLayout)
                    .WithControlsHavingToMatchPath($"<{definition.DeviceLayout}>")
                    .WithControlsExcluding("<Pointer>/position")
                    .WithControlsExcluding("<Touchscreen>/*/position")
                    .WithControlsExcluding("<Keyboard>/anyKey")
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithMatchingEventsBeingSuppressed()
                    .WithTimeout(8)
                    .OnMatchWaitForAnother(0.1f)
                    .OnApplyBinding((_, path) => selectedPath = path)
                    .OnComplete(_ => Finish(selectedPath, finished))
                    .OnCancel(_ => Finish(null, finished));
                if (!definition.DefaultPath.EndsWith("/delta", StringComparison.OrdinalIgnoreCase))
                    operation.WithControlsExcluding("<Pointer>/delta");
                operation.Start();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Finish(string path, Action<string> finished)
        {
            Dispose();
            finished(path);
        }

        public void Dispose()
        {
            if (active != this) return;
            active = null;
            finishedFrame = Time.frameCount;
            operation?.Dispose();
            operation = null;
            focus?.Dispose();
            focus = null;
        }
    }
}
