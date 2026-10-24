# Scene and player scaffolding

Use **Tools > UnscriptedEngine > Create Controller and Pawn...** to enter a name,
choose an existing folder under `Assets`, and select a template. The blank template
creates `Scripts/C_Name.cs`, `Scripts/P_Name.cs`, matching networked prefabs, and `PD_Name.asset`.
Scripts go in a `Scripts` subfolder, scenes in a `Scenes` subfolder, and player prefabs
in a `Prefabs` subfolder of the selected output folder. Missing subfolders are created
automatically; existing ones are reused.
The player definition stores the two prefab references together. Runtime possession
is still performed by `UGameMode`; the prefabs are separate network objects.

Enable **Wire active scene** to assign the controller to the scene NetworkManager's
player prefab, assign the pawn to GameMode's default pawn, and register both network
prefabs. The scene must be saved and contain exactly one GameMode and NetworkManager.
This wiring supports Undo and leaves the scene dirty for review and saving. It replaces
the existing default controller and pawn selections; existing prefab registrations remain.

Use **Create Scene Setup...** to create `GM_Name`, `GI_Name`, `GS_Name`, and `NM_Name`
scripts under `Scripts` and `Scenes/S_Name.unity`. Optionally select a generated player definition to wire
the pair during scene creation. `NP_Name.asset` holds network prefab registrations.
NetworkManager includes Unity Transport; GameMode and GameState include NetworkObjects.
GameInstance is a separate root so its persistence does not retain scene game state.
The tool prompts to save current scenes, then opens the new scene by itself.

The new scene contains framework objects only. Add environment, camera, and gameplay
as needed. Start networking through your own UI/code using `StartHost`, `StartClient`,
or `StartServer`. Add the scene to your build profile when needed. Blank player prefabs
have no movement, camera, input bindings, collision, or transform synchronization.

## Compilation and validation

Generation writes sources first. Once Unity compiles them, generation resumes and
creates scene/prefab assets. Pending work survives script domain reloads during the
current Editor session. If compilation fails, fix Console errors and click **Finish
Pending Generation** in either window. **Cancel Pending Generation** clears the job
but retains source files; it never removes scripts you may have edited. Restarting the
Editor clears the pending session job. If cancelled, generate the same name again
and choose whether to skip or replace the retained files.

Existing outputs trigger a shared **Skip Existing / Replace Existing / Cancel** prompt.
The choice applies to all listed conflicts. Skip retains their contents and reuses existing
prefabs and definitions where needed; missing outputs are still generated. A skipped scene
is not rebuilt or opened. Replace overwrites listed outputs, including custom source code
and scene contents, while retaining asset GUIDs. Replacement is not covered by scene Undo.
Conflicting script names outside the exact target script paths are still rejected.
Select a runtime folder
outside `Editor` and `Resources`. If the folder belongs to a custom assembly definition,
that assembly must reference `UnscriptedEngine` and `Unity.Netcode.Runtime`. Generated
classes use the global namespace. File names and class names use `PREFIX_Name`.

Asset failures roll back assets from that attempt and retain source files for repair
and retry. File creation itself is not part of scene Undo.

## UI setup

Use **Tools > UnscriptedEngine > Create UI...**, enter a name, choose a root output
folder, and select `UCanvasComponent` or `UWidgetComponent`.

| Extension | Script | Prefab |
| --- | --- | --- |
| Canvas | `Scripts/UI/UC_Name.cs` | `Prefabs/UI/UC_Name.prefab` |
| Widget | `Scripts/UI/Widgets/UW_Name.cs` | `Prefabs/UI/Components/UW_Name.prefab` |

All paths are relative to the selected root. Missing folders are created recursively.
The same Skip Existing / Replace Existing / Cancel prompt handles output collisions.
Generated scripts import `UnscriptedEngine` and `UnscriptedEngine.UI`.

Screen and widget prefabs contain a stretching RectTransform, CanvasGroup, and their
respective generated subclass. Both roots use the UI layer. UCanvasController owns
the Unity Canvas, scaling, raycasting, and screen layers.

Create and initialize a UCanvasController for the local player. Use Show or BindToggle
to display screen prefabs. Place reusable widgets beneath a screen; they register
membership automatically. Your scene needs one EventSystem with the appropriate
input module. It is not embedded in each prefab. Call base implementations when
overriding OnEnable, OnDisable, OnDestroy, or OnTransformParentChanged.

The tool lives in the optional `UnscriptedEngine.UI.Editor` assembly; it requires
Unity UI 2.0 or later. Custom output assembly definitions must reference
`UnscriptedEngine` and `UnscriptedEngine.UI`.

## Adding presets

This project's **FirstnThird Person** preset supports toggling between first-person and third-person views and is registered by
`Assets/Editor/FirstThirdPersonScaffoldTemplate.cs`. It uses the current
`Assets/Prefabs/Default/DefaultPawn.prefab` and `PlayerController.prefab`, including
camera hierarchy, movement, equipment, UI references, and assigned input actions.
Generated `C_Name` and `P_Name` inherit `PlayerController` and `DefaultPlayerPawn`.
These presets therefore depend on the project's runtime classes and referenced assets;
they are not standalone copies of those scripts. Future base-class changes apply to
generated subclasses; prefab configuration is copied at generation time.
Generate into the project's default runtime assembly (Assembly-CSharp), since custom
assembly definitions cannot reference these project classes in Assembly-CSharp.

For other prefab-backed presets, implement `IPlayerPrefabScaffoldTemplate` and provide
the two prefab assets and their inheritable root component types. The builder clones
each hierarchy, copies serialized fields to the generated subclass, and remaps local
references to the replaced root component. Original prefab assets are not edited.

Implement `IPlayerScaffoldTemplate` in an Editor assembly referencing
`UnscriptedEngine.Editor`. Give it a public parameterless constructor. The player
window discovers implementations through `TypeCache`.

- `DisplayName` labels the preset.
- `ControllerBody` and `PawnBody` supply code inside generated subclasses. Use fully
  qualified types for other namespaces; generated scripts include `using UnscriptedEngine;`.
  Use the framework's inherited lifecycle hooks.
- `Configure(controller, pawn)` attaches and wires components before prefabs are saved.
  Operate only on the supplied roots and their children. Keep NetworkObjects and generated
  controller/pawn components at the roots. Do not create external assets in this hook.

First-person, third-person, and top-down presets can each supply their own movement,
camera, and input setup without changing the generation queue or scene builder.
