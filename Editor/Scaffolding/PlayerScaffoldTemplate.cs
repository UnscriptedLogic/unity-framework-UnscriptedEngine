using System;
using UnityEngine;

namespace UnscriptedEngine.Editor.Scaffolding
{
    /// <summary>
    /// Implement in an Editor assembly with a public parameterless constructor to add a preset.
    /// Bodies go inside generated UController/UPawn subclasses with using UnscriptedEngine.
    /// Use fully qualified names for types from other namespaces.
    /// Configure adds camera, movement, input, and other preset-specific prefab components.
    /// </summary>
    public interface IPlayerScaffoldTemplate
    {
        string DisplayName { get; }
        string ControllerBody { get; }
        string PawnBody { get; }
        void Configure(GameObject controller, GameObject pawn);
    }

    /// <summary>Optional preset sources. Generated subclasses retain the source components' behavior.</summary>
    public interface IPlayerPrefabScaffoldTemplate : IPlayerScaffoldTemplate
    {
        Type ControllerBaseType { get; }
        Type PawnBaseType { get; }
        GameObject ControllerPrefab { get; }
        GameObject PawnPrefab { get; }
    }

    public sealed class BlankPlayerScaffoldTemplate : IPlayerScaffoldTemplate
    {
        public string DisplayName => "Blank controller and pawn";
        public string ControllerBody => "";
        public string PawnBody => "";
        public void Configure(GameObject controller, GameObject pawn) { }
    }
}
