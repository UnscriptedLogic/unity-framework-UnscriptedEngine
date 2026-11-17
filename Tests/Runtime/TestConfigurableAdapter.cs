using UnityEngine;
using UnscriptedEngine.Configuration;

namespace UnscriptedEngine.Tests
{
    public sealed class TestConfigurableAdapter : MonoBehaviour, IConfigurableFieldAdapter
    {
        public int Number = 2;
        public bool TrySetConfiguration(string fieldName, object value, out string reason)
        {
            reason = "Only nonnegative Number values are accepted.";
            if (fieldName != nameof(Number) || value is not int number || number < 0) return false;
            Number = number;
            reason = null;
            return true;
        }
    }
}
