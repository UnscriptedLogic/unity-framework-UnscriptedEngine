# Saving

Composable gameplay saving lives in `UnscriptedEngine.Saving`.

Start with [the save-system guide](../../Docs/SaveSystem.md) for setup, examples, browser behavior, migration, failure recovery, limitations, and extension points.

Import **Save Counter** from the package's Samples tab for a working component and UI-button methods.

- `SaveService`: instance-owned slot coordinator, dirty tracking, two-generation recovery.
- `SaveModule<T>` / `ISaveModule`: explicit feature capture and prepared restoration.
- `ISaveSerializer`: payload and envelope encoding; `JsonSaveSerializer` is the default.
- `ISaveStorage`: persistence; `FileSaveStorage` and `WebSaveStorage` ship with the package.
- `SaveStorage.CreateDefault()`: chooses files in the Editor/native players and direct IndexedDB in Web players.

Settings persistence remains separate. Adding gameplay saving does not change `SettingsRuntime`, install a global singleton, scan scenes, or save automatically.
