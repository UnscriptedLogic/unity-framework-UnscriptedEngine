# Scene and player scaffolding

Use **Tools > UnscriptedEngine > Create Controller and Pawn...** to enter a name,
choose an existing folder under `Assets`, and select a template. The blank template
creates `Scripts/C_Name.cs`, `Scripts/P_Name.cs`, matching networked prefabs, and `PD_Name.asset`.
Scripts go in a `Scripts` subfolder and scenes in a `Scenes` subfolder of the selected
output folder. Missing subfolders are created automatically; existing ones are reused.
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
Editor clears the pending session job. If cancelled, remove or rename the retained
scripts before generating that name again.

Existing outputs and conflicting script names are rejected. Select a runtime folder
outside `Editor` and `Resources`. If the folder belongs to a custom assembly definition,
that assembly must reference `UnscriptedEngine` and `Unity.Netcode.Runtime`. Generated
classes use the global namespace. File names and class names use `PREFIX_Name`.

Asset failures roll back assets from that attempt and retain source files for repair
and retry. File creation itself is not part of scene Undo.

## Adding presets

Implement `IPlayerScaffoldTemplate` in an Editor assembly referencing
`UnscriptedEngine.Editor`. Give it a public parameterless constructor. The player
window discovers implementations through `TypeCache`.

- `DisplayName` labels the preset.
- `ControllerBody` and `PawnBody` supply code inside generated subclasses. Use fully
  qualified types and the framework's inherited lifecycle hooks.
- `Configure(controller, pawn)` attaches and wires components before prefabs are saved.
  Operate only on the supplied roots and their children. Keep NetworkObjects and generated
  controller/pawn components at the roots. Do not create external assets in this hook.

First-person, third-person, and top-down presets can each supply their own movement,
camera, and input setup without changing the generation queue or scene builder.
