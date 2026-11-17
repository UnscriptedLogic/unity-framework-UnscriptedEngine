using System;
using UnityEngine;

namespace UnscriptedEngine.Tests
{
    public sealed class TestConfigurableFields : MonoBehaviour
    {
        public enum Mode { First, Second }
        public bool Flag = true;
        [Range(0f, 10f)] public float Amount = 2f;
        [SerializeField] private int count = 7;
        public Mode Choice = Mode.First;
        public string Text = "hello";
        [HideInInspector] public bool Hidden;
        [NonSerialized] public bool Transient;
        public Vector3 Unsupported;
        public int Count => count;
    }
}
