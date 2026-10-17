using UnityEngine;

namespace UnscriptedEngine.Editor.Scaffolding
{
    /// <summary>
    /// Implement in an Editor assembly with a public parameterless constructor to add a preset.
    /// Bodies go inside generated UController/UPawn subclasses. Use fully qualified type names.
    /// Configure adds camera, movement, input, and other preset-specific prefab components.
    /// </summary>
    public interface IPlayerScaffoldTemplate
    {
        string DisplayName { get; }
        string ControllerBody { get; }
        string PawnBody { get; }
        void Configure(GameObject controller, GameObject pawn);
    }

    public sealed class BlankPlayerScaffoldTemplate : IPlayerScaffoldTemplate
    {
        public string DisplayName => "Blank controller and pawn";
        public string ControllerBody => "";
        public string PawnBody => "";
        public void Configure(GameObject controller, GameObject pawn) { }
    }
}
