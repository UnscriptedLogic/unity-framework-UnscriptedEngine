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
        public Vector2 Position2 = new Vector2(1f, 2f);
        public Vector3 Position3 = new Vector3(1f, 2f, 3f);
        public Vector4 Position4 = new Vector4(1f, 2f, 3f, 4f);
        public Quaternion Rotation = Quaternion.identity;
        public Color Tint = Color.white;
        public AnimationCurve Unsupported;
        public int Count => count;
    }
}
