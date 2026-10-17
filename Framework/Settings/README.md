# Modular settings

Settings definitions are assets. Scene modules activate groups of definitions. A persistent `SettingsService` owns effective values, validates writes, and notifies consumers. The generated uGUI panel displays active groups without knowing gameplay code.

## Getting started in this project

The project includes a ready-to-use catalog at `Assets/Resources/Settings/GeneralSettings.asset`:

```text
General
  Audio
    Master volume
    Music volume
    Sound effects volume
  Controls
    Mouse sensitivity
    Invert mouse Y
```

The default GameInstance prefab and StadiumTrack scene contain a **General Settings Module** child. The existing `SettingsUI` modal now creates a generated panel. Open the settings menu as usual. Disable or remove the module child to hide its settings and revert their effective values to defaults.

For another scene:

1. Add `Assets/Prefabs/Default/GeneralSettingsModule.prefab`, or choose **GameObject > Unscripted > Settings > General Settings Module**.
2. Use the existing game settings modal. For an independent test panel, choose **GameObject > Unscripted > Settings > Settings Preview Canvas**.
3. Enter Play Mode. Change a value, disable the module, then enable it again. The saved override becomes effective again when the module returns.

Keep modules outside menu objects that are hidden or destroyed when the menu closes. Place persistent general modules under the persistent GameInstance; keep prototype modules in their prototype scenes. The preview canvas does not acquire gameplay input focus or unlock a gameplay-locked cursor; use the existing modal for in-game interaction.

Master volume drives `AudioListener.volume`. Mouse sensitivity multiplies the pawn's existing pointer sensitivity; invert Y applies to pointer input. Music and sound-effects values are exposed and saved, but need consumers wired to your game's audio mixer or audio sources.

## Using settings in code

Typed catalogs provide autocomplete. Their leaves are definitions, not mutable values:

```csharp
var settings = SettingsRuntime.Service;
var generalSettings = GeneralSettingsCatalog.Current;

float sensitivity = settings.Get(generalSettings.Controls.MouseSensitivity);
bool accepted = settings.TrySet(generalSettings.Controls.MouseSensitivity, 1.5f);
settings.Reset(generalSettings.Controls.MouseSensitivity);
```

Framework types use `UnscriptedEngine.Settings`. The General catalog is project code, not part of the reusable package.

Consumers do not need an active module or UI to read defaults. Writes require an active module and permission for the definition's scope. `TrySet` returns false for inactive settings, invalid value types, non-finite numbers, invalid choices, or unauthorized writes. Numeric values are clamped to their authored range; text is limited to its maximum length.

Subscribe once and apply the current value immediately:

```csharp
using System;
using UnityEngine;
using UnscriptedEngine.Settings;

public sealed class ExampleSettingsConsumer : MonoBehaviour
{
    [SerializeField] private FloatSettingDefinition springStrength;
    private IDisposable subscription;
    private float strength;

    private void OnEnable()
    {
        subscription = SettingsRuntime.Service.Observe(
            springStrength, value => strength = value);
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
```

`Observe` emits the current effective value by default. Set `emitCurrent: false` for change-only observation. Equal effective values do not generate notifications. Dispose subscriptions when their owner stops using them. Unity-object consumers should unsubscribe before destruction.

Polling `Get` is also appropriate when processing an input event. Every write must pass through the service; do not mutate definition assets to change runtime values.

## Appending your own settings

1. Create a prototype folder, for example `Assets/Prototypes/Grapple/Settings`.
2. Choose **Create > Unscripted Engine > Settings > Float**, **Integer**, **Boolean**, or **Text or Choice**.
3. Give the definition a permanent ID such as `grapple.experimental.spring-strength`, a display name, default, and range. Set its scope and optional description, editor hint, advanced flag, and sort order.
4. Create an **Experimental** group containing the definition. Create a **Grapple Prototype** group with Experimental in its Children list.
5. Add `SettingsModule` to a scene object and assign the Grapple Prototype group.
6. Reference the definition from a consumer and use `Get`, `TrySet`, or `Observe`.

The panel discovers the module, builds nested collapsible groups, and hides empty groups. Search matches labels, descriptions, IDs, and group paths. Advanced definitions remain hidden until **Show advanced settings** is enabled. Closing a group only collapses its UI; it does not deactivate settings.

Use unique IDs across the application. Labels and UI grouping can change without changing saved identity. Changing an ID creates a new preference; renaming IDs requires an explicit migration. Two different definition assets with the same ID are rejected, including when read without a module. Reusing the same definition in multiple modules is supported.

Group cycles and missing references are rejected before activation. Lists are authored configuration: change them outside Play Mode. To switch a running module, call `module.SetGroup(otherGroup)`.

### Optional typed prototype catalogs

Asset-only definitions need no central registration code. For convenient dot access, add a small catalog:

```csharp
using System;
using UnityEngine;
using UnscriptedEngine.Settings;

[CreateAssetMenu(menuName = "Prototypes/Grapple Settings Catalog")]
public sealed class GrappleSettingsCatalog : ScriptableObject
{
    [Serializable]
    public sealed class ExperimentalSettings
    {
        public FloatSettingDefinition SpringStrength;
        public BoolSettingDefinition PreserveMomentum;
    }

    public SettingsGroup Root;
    public ExperimentalSettings Experimental;
}
```

Assign the same definition assets to the catalog and its groups. Then read `settings.Get(grappleSettings.Experimental.SpringStrength)`. C# member access comes from authored fields; Inspector names do not generate C# properties. Catalog references do not activate settings; a module still controls activation.

## Removal, defaults, and persistence

| State | Effective local value | UI |
|---|---|---|
| No active module | Definition default | Hidden |
| Active, no override | Definition default | Visible |
| Active, valid saved override | Saved override | Visible |
| Last registration removed | Definition default; notify if changed | Removed |
| Module returns | Retained override | Visible |

Registrations are reference-counted. Removing one owner does not deactivate settings still owned by another. Disposal is idempotent. Reset removes the stored override, so future default changes take effect.

The runtime saves local preferences to `Application.persistentDataPath/Settings.v2.json` through the existing versioned `JsonSettingsStore`. Changed preferences are saved at most every two seconds, when a panel closes, on application pause, and on quit. Call `SettingsRuntime.Save()` for an explicit checkpoint. Failure leaves the dirty flag set for a later retry. Storage uses temporary-file replacement.

Unknown IDs are retained to preserve preferences for temporarily absent prototypes. Deleting a prototype does not automatically purge its saved preferences. Invalid known values fall back to defaults. Unsupported file schema versions are rejected by the JSON store. The store does not implement automatic migrations between arbitrary future schemas.

The project imports the old `GameSettings.json` into stable IDs when no v2 file exists. The old file remains untouched. The legacy GameSettings asset is only an import seed; edit the new definition assets to change defaults. Imported sensitivity is clamped to the new authored 0.01–10 range.

## Customization and extending

### Themes and custom controls

Create **Unscripted Engine > Settings > UI Theme** and assign it to `SettingsUI` or `GeneratedSettingsPanel`. Each mapping selects a prefab containing a `SettingsWidget` subclass. Selection order is:

1. An exact definition reference.
2. A matching nonempty editor hint, with no definition reference.
3. A value-kind mapping, with neither definition nor editor hint.
4. The built-in template control if no mapping matches.

First matching entry within each tier wins. This lets one setting use a custom editor without affecting other settings of the same type. Built-in kinds are Boolean, Float, Integer, Text, Choice, and Custom. A string definition with nonempty Choices uses a dropdown; choices are saved as strings, not array indices.

Example prefab component:

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.UI.Settings;

public sealed class CustomBoolSettingsWidget : SettingsWidget
{
    [SerializeField] private Toggle toggle;

    protected override void OnBound()
    {
        toggle.interactable = Service.CanWrite(Definition);
        toggle.onValueChanged.AddListener(ChangeValue);
    }

    protected override void DisplayValue(object value)
        => toggle.SetIsOnWithoutNotify((bool)value);

    private void ChangeValue(bool value) => Write(value);

    protected override void OnUnbound()
        => toggle.onValueChanged.RemoveListener(ChangeValue);
}
```

The base widget manages value subscriptions, including disable/re-enable. `OnUnbound` must remove listeners owned by your widget. Do not remove unrelated listeners. Refresh controls without notification to avoid write loops. `Write` always refreshes the displayed effective value, including when a write is rejected.

The template shell includes search, advanced visibility, scrolling, and collapsible headings. To replace its shell, provide a custom Content RectTransform on `GeneratedSettingsPanel` and wire your own search/advanced controls to `SetSearch` and `SetShowAdvanced`. Content needs a suitable layout group and, inside a ScrollRect, a content size fitter. Theme mappings replace setting rows, not group headings. Replace or extend the panel for a fully bespoke navigation layout.

### New value types

Create a concrete `SettingDefinition<T>` subclass for serializable values. Implement normalization and validity rules; override serialization when JsonUtility cannot represent the type. Provide a matching custom widget. Runtime values should be immutable values or immutable snapshots; mutating an object obtained through `Get` bypasses notification and is unsupported.

Keep stable IDs independent from labels and group hierarchy. Service validation is the shared boundary for UI, console commands, file imports, and transport adapters. Theme controls may offer stricter interaction affordances but cannot replace service validation.

### Alternative storage or service ownership

Construct `new SettingsService()` for tests or an explicitly owned service. `ExportPreferences` and `LoadPreferences` provide the adapter boundary. `LoadPreferences` replaces the full preference set; merge first if importing only some settings. It does not mark the global runtime dirty. Use `SettingsRuntime.ImportPreferences` when importing into the global service and saving afterward.

The supplied modules and panel use `SettingsRuntime.Service`. Dependency injection for multiple simultaneous profiles requires custom module/panel adapters. Definition and group assets are shared metadata, not the storage backend.

## Server-sided settings

Set a definition's scope to **ServerSession**. Its values are never saved as local user preferences. The server/host can write active settings; clients display them read-only and `TrySet` rejects changes even if called outside UI.

For Netcode for GameObjects:

1. Add one `SettingsNetworkBridge` to a server-owned NetworkObject that lives for the session and is visible to every client.
2. Assign all groups containing server settings to its Catalogs list on both server and client builds. These catalogs define the known schema; they do not activate settings.
3. Spawn the object through your existing network bootstrap. Keep it alive across scene transitions, using your game's persistent network-object setup.
4. Activate rule overrides with server-side scene modules. Use identical IDs/types/ranges/defaults on all peers.

The bridge sets the service role, sends initial values to joining clients, and replicates later effective-value changes. Only the server sends authoritative value RPCs. There is no client write-request RPC; a client can request only a current snapshot. Add a separate, permission-checked server command if you later need remote administrator edits.

Removing a client's local UI module cannot reset replicated server rules. Removing the final server module resets effective values to defaults and broadcasts those defaults. Despawning the bridge returns to standalone mode and clears session overrides. Local preferences remain intact.

Before the bridge spawns, the service is standalone. If client bootstrap can expose gameplay or settings before network spawn, call `SetRole(SettingsRole.Client)` when joining starts and restore standalone on connection failure. Do not enable authoritative gameplay until required initial settings arrive; the basic bridge sends individual values and does not provide a snapshot-ready barrier. Multiple bridges or multiple network sessions sharing the same static service are unsupported.

For another transport, set the role during connection setup, authenticate the server sender, deserialize against known definitions, and call `ApplyServerValue`. That method is an integration hook, not a security boundary against arbitrary local code. Gameplay rules must be enforced on the actual authoritative server.

## Limitations and deliberate scope

- All service operations and callbacks run on Unity's main thread. Marshal background/network worker results before calling them.
- Scene reload must remain enabled when domain reload is disabled. Disabling both reloads is unsupported.
- Changes apply immediately. No draft/apply/cancel transaction, restart-required policy, or graphics rollback timer is supplied.
- Numeric defaults/ranges must be sensible and finite. Integer sliders use uGUI floats; use a custom integer input for values beyond exact float integer precision.
- The template uses legacy uGUI Text and the built-in font. It is a functional prototype, not a localized or accessibility-audited production menu. Localized labels, controller focus restoration after rebuilds, and specialized key-binding capture need custom UI.
- Theme prefabs should have suitable RectTransforms/layout components. Unknown custom value types display a missing-editor message until mapped.
- The panel rebuilds after structural/search changes. Very large catalogs need virtualization; definitions intentionally shared across visible groups can appear in more than one location.
- No general reflection-based registration, generated C# catalogs, arbitrary asset hot-editing during Play Mode, cross-setting dependency system, or automatic ID migration.
- The basic network bridge is suitable for small settings catalogs. It has no batching, revision protocol, host migration, client admin requests, or synchronized apply transaction.
- Saving depends on filesystem replacement support on the target platform. The current adapter targets the existing desktop-style JSON storage path.
- Music/SFX routing belongs to your game. Stored values alone do not configure an AudioMixer.

## Validation

`SettingsServiceTests` covers inactive defaults, owner removal/reactivation, overlapping groups, validation, subscription disposal, duplicate IDs, nested groups/cycles, JSON round trips, resets, choices, and client authority. `SettingsWidgetTests` enters Play Mode and covers editable numeric controls, invalid text, reset, disable/re-enable subscriptions, read-only replicated choices, rebinding, advanced filtering, search, and live module removal.

Validated with Unity 6000.4.8f1 in an isolated consumer project: all 17 settings tests passed. General catalog defaults and prefab references were imported and checked; the generated panel was rendered and visually inspected. Project runtime scripts also compiled against the updated framework. This does not establish end-to-end multiplayer transport behavior or every target platform.

In Unity Test Runner, run the two fixtures in Edit Mode. Network transport integration and target-platform visual/input behavior should also be checked in the actual game: connect a late client, change a server rule, remove its client UI module, remove its server module, and disconnect. Confirm that only the server controls the rule and that local preferences survive.
