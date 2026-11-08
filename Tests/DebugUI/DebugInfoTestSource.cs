using UnityEngine;
using UnscriptedEngine.UI;

namespace UnscriptedEngine.Tests
{
    public sealed class DebugInfoTestSource : MonoBehaviour, IDebugInfoSource
    {
        private int collections;
        public int Collections => collections;

        public void CollectDebugInfo(DebugInfoWriter writer)
        {
            collections++;
            writer.Add("Health", 42);
        }
    }
}
