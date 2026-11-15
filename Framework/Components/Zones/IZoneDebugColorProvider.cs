using UnityEngine;

namespace UnscriptedEngine.Gameplay
{
    /// <summary>Optional color supplied by an enabled component beside ColliderZone. First provider wins.</summary>
    public interface IZoneDebugColorProvider
    {
        bool TryGetZoneDebugColor(out Color color);
    }
}
