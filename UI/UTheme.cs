using System;
using TMPro;
using UnityEngine;

namespace UnscriptedEngine
{
    [CreateAssetMenu(fileName = "New Theme", menuName = "ScriptableObjects/UnscriptedEngine/Create New Theme")]
    public class UTheme : ScriptableObject
    {
        [System.Serializable]
        public class ColorPalette
        {
            public Color main;
            public Color light;
            public Color dark;
        }

        [SerializeField] private ColorPalette primaryPalette;

        [SerializeField] private TMP_FontAsset primaryFont;

        public ColorPalette PrimaryPalette => primaryPalette;
        public TMP_FontAsset PrimaryFont => primaryFont;
    }
}