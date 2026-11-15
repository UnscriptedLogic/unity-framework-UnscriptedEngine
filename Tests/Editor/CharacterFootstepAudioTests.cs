using System.Reflection;
using NUnit.Framework;
using UnscriptedEngine.Audio;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class CharacterFootstepAudioTests
    {
        [Test]
        public void RenderFramesBetweenMovementTicksPreserveStepProgress()
        {
            GameObject host = new GameObject("Footstep cadence test");
            try
            {
                CharacterFootstepAudio footsteps = host.AddComponent<CharacterFootstepAudio>();
                MethodInfo accumulate = typeof(CharacterFootstepAudio).GetMethod(
                    "AccumulateStepDistance", BindingFlags.Instance | BindingFlags.NonPublic);
                int steps = 0;
                for (int movementTick = 0; movementTick < 16; movementTick++)
                {
                    if ((bool)accumulate.Invoke(footsteps, new object[] { 0.12f, 36f, 1f / 150f })) steps++;
                    Assert.IsFalse((bool)accumulate.Invoke(footsteps, new object[] { 0f, 36f, 1f / 150f }));
                    Assert.IsFalse((bool)accumulate.Invoke(footsteps, new object[] { 0f, 36f, 1f / 150f }));
                }
                Assert.AreEqual(1, steps, "1.92 metres should trigger a step despite unchanged render samples.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void StoppingResetsProgressAndBlockedMovementDoesNotAdvanceIt()
        {
            GameObject host = new GameObject("Footstep stopped test");
            try
            {
                CharacterFootstepAudio footsteps = host.AddComponent<CharacterFootstepAudio>();
                MethodInfo accumulate = typeof(CharacterFootstepAudio).GetMethod(
                    "AccumulateStepDistance", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsFalse((bool)accumulate.Invoke(footsteps, new object[] { 1f, 36f, 0.02f }));
                Assert.IsFalse((bool)accumulate.Invoke(footsteps, new object[] { 0f, 0f, 0.02f }));
                Assert.IsFalse((bool)accumulate.Invoke(footsteps, new object[] { 1f, 36f, 0.02f }));
                for (int frame = 0; frame < 100; frame++)
                    Assert.IsFalse((bool)accumulate.Invoke(footsteps, new object[] { 0f, 36f, 0.02f }));
                Assert.IsTrue((bool)accumulate.Invoke(footsteps, new object[] { 0.9f, 36f, 0.02f }));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
