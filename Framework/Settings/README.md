# Code-defined, scene-activated settings

Declare settings in C#. Add catalog-provider components to a scene-local **Settings Container** GameObject. The persistent service owns values, defaults, validation, notifications, and saved preferences. Code declarations alone never activate settings.

## Getting started

This project includes:

- `GeneralSettings.cs`: Audio, Controls, and Camera definitions, with typed member access.
- `GeneralSettingsProvider`: activates all General settings, including nested catalogs.
- `CameraSettingsProvider`: activates only General/Camera.
- `Assets/Prefabs/Default/SettingsContainer.prefab`: scene-root container with a General provider.
- `SettingsUI`: existing game modal using the generated settings panel.
- `FPSCameraSettingsBinding`: connects Dynamic FOV to `FPSCameraTweaks`; included in DefaultPawn.

Choose **GameObject > Unscripted Engine > Settings > General Settings Container**, or drag the container prefab into your scene as a root object. For a camera-only prototype, use **Camera Settings Container** instead.

For an independent test menu, choose **GameObject > Unscripted Engine > Settings > Settings Preview Canvas**. This preview does not acquire gameplay input focus or unlock a gameplay-locked cursor; use the existing game modal for gameplay interaction.

StadiumTrack, FPS, and the RAGRaceSmoke test scene have been migrated to root containers. The persistent GameInstance prefab no longer contains a settings provider.

Keep containers outside persistent GameInstance objects and outside menus that are disabled when closed. Parenting a container beneath a DontDestroyOnLoad object makes it survive scene changes. Its placement determines its lifetime.

## Using settings

```csharp
SettingsService settings = SettingsRuntime.Service;

float sensitivity = settings.Get(GeneralSettings.Controls.MouseSensitivity);
bool accepted = settings.TrySet(GeneralSettings.Camera.DynamicFov, false);
settings.Reset(GeneralSettings.Camera.DynamicFov);
```

Use `UnscriptedEngine.Settings` for framework types. GeneralSettings is project code. Catalog members are immutable definitions; runtime values belong to the service.

A read or subscription without an active provider returns the definition's default. Writes require an active registration and permission. Rejected writes return false. Numeric values are clamped; NaN/infinity and incorrect value types are rejected. Choice values must match their authored strings.

A small consumer can observe a definition:

```csharp
using System;
using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.Settings;

[RequireComponent(typeof(FPSCameraTweaks))]
public sealed class ExampleCameraBinding : MonoBehaviour
{
    private IDisposable subscription;

    private void OnEnable()
    {
        FPSCameraTweaks cameraTweaks = GetComponent<FPSCameraTweaks>();
        subscription = SettingsRuntime.Service.Observe(
            GeneralSettings.Camera.DynamicFov,
            cameraTweaks.SetDynamicFovEnabled);
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
```

The project already supplies this binding as FPSCameraSettingsBinding; do not add both. It changes only dynamic FOV, leaving head bob, tilt, and shake intact. Disabling dynamic FOV restores the captured original lens values immediately.

Observe applies the current effective value immediately by default, then reports changes. Pass `emitCurrent: false` for change-only observation. Equal effective values do not notify. Dispose subscriptions when the consumer stops using them. Polling Get while handling input is also supported.

## Appending your own prototype

Create a catalog in the prototype's scripts folder:

```csharp
using UnscriptedEngine.Settings;

[SettingsCatalogRoot]
public static class GrappleSettings
{
    public static class Experimental
    {
        public static readonly Setting<bool> PreserveMomentum = Setting.Bool(
            id: "grapple.experimental.preserve-momentum",
            defaultValue: true,
            label: "Preserve momentum",
            group: "Grapple/Experimental",
            description: "Keep existing velocity when releasing the grapple.");

        public static readonly Setting<float> RopeLength = Setting.Float(
            id: "grapple.experimental.rope-length",
            defaultValue: 20f,
            min: 1f,
            max: 100f,
            label: "Rope length",
            group: "Grapple/Experimental",
            description: "Maximum rope length in world units.");
    }
}
```

Create its provider in a matching `GrappleSettingsProvider.cs` file:

```csharp
using System;
using UnityEngine;
using UnscriptedEngine.Settings;

[AddComponentMenu("Unscripted Engine/Settings/Grapple Settings Provider")]
public sealed class GrappleSettingsProvider : SettingsProvider
{
    protected override Type CatalogType => typeof(GrappleSettings);
}
```

Add this component to Prototype 1's Settings Container. Add a Racing provider to Prototype 2's container instead. Adding another static readonly declaration to GrappleSettings requires no new asset, Inspector reference, or provider change.

Consumers access `GrappleSettings.Experimental.RopeLength`. UI grouping comes from `group: "Grapple/Experimental"`, independently of C# nesting. Moving a declaration in C# or changing its UI group does not change saved identity.

Each field must be **public static readonly**. Providers discover only their explicitly selected type and public nested types. No assembly-wide runtime scan or automatic global registration occurs. Discovery results are cached. Mutable or null definition fields and conflicting IDs are rejected.

Use `[SettingsCatalogRoot]` on each outer catalog. The package's editor linker processor preserves that catalog and its nested types in player builds. This prevents reflection-only declarations being stripped. The attribute does not activate settings. Alternatively, override GetDefinitions to return an explicitly constructed read-only collection with direct references, avoiding reflected discovery.

## Declaration options

| Factory | Value | Additional arguments |
|---|---|---|
| Setting.Bool | bool | defaultValue |
| Setting.Float | float | defaultValue, min, max |
| Setting.Int | int | defaultValue, min, max |
| Setting.Text | string | defaultValue, optional maxLength |
| Setting.Choice | string | defaultValue, choices |

Every factory takes a permanent ID, label, and slash-separated group path. Optional arguments: description, scope, editorHint, advanced, and order.

- IDs must be application-wide unique. Reuse the same static declaration when sharing settings.
- Defaults must be valid. Numeric defaults are normalized within the authored range.
- Group paths cannot contain empty sections. Groups are built from active definitions and merged by exact path.
- Groups sort alphabetically. Settings sort by order, then label.
- Advanced hides a row until Show advanced settings is enabled; it does not deactivate the value.
- Editor hints select custom theme widgets without changing value type.
- Choice strings are saved as values, not indices. Reordering is safe; renaming a choice needs migration.

For example:

```csharp
public static readonly Setting<string> Difficulty = Setting.Choice(
    id: "racing.session.difficulty",
    defaultValue: "normal",
    choices: new string[] { "easy", "normal", "hard" },
    label: "Difficulty",
    group: "Racing/Rules",
    scope: SettingScope.ServerSession);
```

Ordinary gameplay fields remain separate. Declarations do not automatically find component fields with similar names. A binding, subscription, or Get call connects the preference to behavior.

## Scene lifetime and defaults

| State | Local effective value | UI |
|---|---|---|
| Definition referenced but no provider | Default | Hidden |
| Provider active, no saved override | Default | Visible |
| Provider active, saved override exists | Validated saved value | Visible |
| Last provider disabled or unloaded | Default; notify if changed | Removed |
| Provider returns | Retained override | Visible |

Scene providers register on enable and unregister on disable/destruction. They do not make themselves persistent. Disabling a whole container disables all its providers.

Registrations are reference-counted. In additive scenes, both sets remain active. Unloading one scene leaves shared definitions active if another scene still registers them. UI merges shared category paths and displays a definition once even when multiple providers register it.

Referencing inactive settings in code does not bring their UI back. Collapse/search controls affect visibility only. Returning to a scene restores its preferences without keeping its container alive.

ServerSession values are an exception on clients: removing a local provider hides their UI but cannot reset replicated authoritative rules.

## Persistence and migration

The filename remains `Application.persistentDataPath/Settings.v2.json`. The existing JsonSettingsStore envelope and per-value JSON payload are unchanged. The five original General IDs and defaults are preserved, so existing preferences load without conversion. Dynamic FOV adds `general.camera.dynamic-fov`, default true.

The service saves local overrides every two seconds while dirty, when the panel closes, on pause, and on quit. Call SettingsRuntime.Save for an explicit checkpoint. Writes use temporary-file replacement; failed saves remain dirty for retry. ServerSession overrides are not local preferences.

Reset removes the saved override. Removing a provider keeps it for later reuse. Unknown IDs remain in the file so temporarily absent prototypes keep their preferences. Arbitrary ID renames and future schema versions still require explicit migrations.

DefaultGameInstance still imports old GameSettings.json when no v2 file exists. Its legacy GameSettings asset is only an import seed. Edit GeneralSettings.cs to change current defaults.

The old per-setting/group assets and old catalog/module scripts were moved outside Unity's imported Assets tree into `Documentation/SettingsMigration/LegacyAssets`. The two unconfigured RubixCore drafts were preserved there too. The draft group had no entries and the draft float had no ID; no active prototype settings were inferred from them.

API migration:
- Replace SettingDefinition<T> with Setting<T>.
- Replace asset references with static catalog members.
- Replace SettingsModule/SetGroup with concrete SettingsProvider components.
- Register now accepts IEnumerable<SettingDefinition>.
- SettingsGroup is a runtime UI tree, not a ScriptableObject.
- Theme exact matches use Setting Id instead of definition asset references.
- Old GeneralSettingsCatalog.Current references become GeneralSettings static references.
- The former GeneralSettingsModule prefab is now SettingsContainer; its prefab GUID is retained.

## Customization and extending

### Theme widgets

Create **Unscripted Engine > Settings > UI Theme** and assign it to SettingsUI or GeneratedSettingsPanel. Theme assets remain useful because they reference visual prefabs; settings themselves no longer need assets.

Mapping priority:
1. Exact Setting Id.
2. Nonempty Editor Hint, with Setting Id empty.
3. Kind, with both ID and hint empty.
4. Built-in template control.

The first match in each tier wins. Custom prefabs derive from SettingsWidget. OnBound configures labels/listeners; DisplayValue refreshes without triggering UI change events; OnUnbound removes listeners owned by that widget. Use Write to request changes through validation and permissions. The base widget manages subscriptions across disable/enable and retains the project's UWidgetComponent message behavior.

Example custom Boolean widget:

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnscriptedEngine.UI.Settings;

public sealed class CustomBoolSettingsWidget : SettingsWidget
{
    [SerializeField, Tooltip("Toggle used to edit the bound Boolean setting.")]
    private Toggle toggle;

    protected override void OnBound()
    {
        toggle.interactable = Service.CanWrite(Definition);
        toggle.onValueChanged.AddListener(ChangeValue);
    }

    protected override void DisplayValue(object value)
    {
        toggle.SetIsOnWithoutNotify((bool)value);
    }

    private void ChangeValue(bool value) => Write(value);

    protected override void OnUnbound()
    {
        toggle.onValueChanged.RemoveListener(ChangeValue);
    }
}
```

Provide Content on GeneratedSettingsPanel to use a custom shell. It needs suitable layout and scroll sizing. Wire custom filters to SetSearch and SetShowAdvanced. Without Content, the template builds its own shell. Theme mappings replace rows; replace/extend the panel for custom category navigation.

### New types and adapters

Derive a concrete immutable definition from Setting<T> for additional value types. Supply DefaultValue, normalization, validation, and a matching widget. Override serialization for types unsupported by JsonUtility. Preserve the serialized contract if changing an existing type.

Prefer immutable value types or immutable snapshots. Mutating a reference returned by Get bypasses validation and notifications.

Construct an isolated SettingsService for tests or custom ownership. ExportPreferences and LoadPreferences form the storage boundary. LoadPreferences replaces the preference set. Merge first for partial imports. SettingsRuntime.ImportPreferences marks the global runtime dirty for saving.

The provided scene providers and UI use SettingsRuntime.Service. Multiple simultaneous local profiles need explicit service injection/custom adapters.

## Server-sided settings

LocalUser is the default scope. Set ServerSession for server-controlled match rules. The service rejects client writes even outside the UI.

The optional Netcode SettingsNetworkBridge uses **Catalog Providers** instead of group assets:

1. Create one server-owned, session-persistent NetworkObject with SettingsNetworkBridge.
2. Add disabled provider components that cover the complete server schema, including settings used by future scenes.
3. Assign those components to Catalog Providers on every peer.
4. Spawn the bridge using the game's network bootstrap.
5. Add enabled scene providers to activate actual rule overrides.

The bridge reads schema from disabled providers without activating them. Server scene providers control effective overrides. The bridge caches schema at spawn, sends current values to late joiners, and replicates later changes. There is no client write RPC; clients can request only an initial snapshot.

Removing the final server registration restores defaults and broadcasts them. Removing a client registration only hides UI. Despawning the bridge clears session overrides and returns to standalone mode; local preferences survive.

Before bridge spawn the service is standalone. If a joining client can expose gameplay/settings early, set Client role when joining begins and restore Standalone on failure. Initial values arrive individually; the bridge has no snapshot-ready barrier. Gate gameplay appropriately in your connection flow.

For another transport, authenticate server messages before calling ApplyServerValue. That method is a transport integration hook, not protection against arbitrary local code. Enforce rules on the actual server.

## Limitations

- Definition/default changes require recompilation. No automatic properties from Inspector labels or gameplay fields.
- Main-thread service only. Marshal background results to Unity's main thread.
- Keep scene reload enabled when disabling domain reload. Disabling both reloads is unsupported.
- No apply/cancel transaction, cross-setting dependency engine, restart policy, or graphics rollback timer.
- The template uses legacy uGUI Text and the built-in font. Localization, accessibility, custom key binding, and production UI polish remain extension work.
- Structural/filter changes rebuild rows. Very large catalogs need virtualization and deliberate focus restoration.
- Integer sliders use floats internally. Use a custom editor beyond exact float integer precision.
- Music/SFX preferences need game-specific audio routing; master volume and pointer controls are already connected.
- The basic network bridge has no batching, versioned snapshots, host migration, client-admin request flow, or multiple-session support.
- File replacement support must be verified on each target platform.
- Preserved metadata generation is tested; an IL2CPP player build and end-to-end multiplayer remain separate validation.

## Validation

Run SettingsServiceTests and SettingsWidgetTests in Unity's Edit Mode test runner. UI tests enter Play Mode. Coverage includes nested catalog discovery, default-only reads, duplicate IDs, shared categories, range/type validation, legacy JSON payloads, resets, role restrictions, real additive scene unloads, override restoration, widget subscriptions, search, and advanced filtering.
