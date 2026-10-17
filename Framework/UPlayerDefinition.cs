using UnityEngine;

namespace UnscriptedEngine
{
    /// <summary>A reusable controller/pawn prefab pairing. Possession remains owned by UGameMode.</summary>
    public sealed class UPlayerDefinition : ScriptableObject
    {
        public UController controllerPrefab;
        public UPawn pawnPrefab;
    }
}
