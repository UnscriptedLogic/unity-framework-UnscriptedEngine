# Settings System

Settings use typed C# definitions, scene-local providers, and one runtime service. This separates setting metadata from current values and lets each scene activate only settings it needs. A code reference alone does not activate a setting.

## Why and architecture ⚙️

- `Setting<T>` holds immutable metadata: stable ID, type, default, group, scope, and UI hints.
- `SettingsProvider` registers one selected catalog while its component is enabled in Play Mode. Its scene lifetime controls which settings are active and visible.
- `SettingsRuntime.Service` owns effective values, validation, change notifications, and local preferences across scene changes.
- `GeneratedSettingsPanel` builds UI from active definitions. Gameplay code reads and observes the same service.
- `SettingsNetworkBridge` optionally synchronizes `ServerSession` values with Netcode for GameObjects.

## Getting started

Declare settings as `public static readonly` members. Keep IDs unique and stable; saved values use IDs as their identity.

```csharp
using UnscriptedEngine.Settings;

[SettingsCatalogRoot]
public static class AudioSettings
{
    public static readonly Setting<float> MasterVolume = Setting.Float(
        id: "audio.master-volume",
        defaultValue: 0.8f,
        min: 0f,
        max: 1f,
        label: "Master volume",
        group: "Audio");
}
```

Create a provider for that catalog, then add it to a scene-local Settings Container GameObject:

```csharp
using System;
using UnscriptedEngine.Settings;

public sealed class AudioSettingsProvider : SettingsProvider
{
    protected override Type CatalogType => typeof(AudioSettings);
}
```

Providers register on enable and unregister on disable or destruction. Keep the container in the scene whose lifetime should control these settings. When final provider unregisters, local value returns to default while saved override stays for later activation. Use `[SettingsCatalogRoot]` so Unity's player linker preserves catalog types discovered through reflection.

## Using settings

```csharp
SettingsService settings = SettingsRuntime.Service;

float volume = settings.Get(AudioSettings.MasterVolume);
bool accepted = settings.TrySet(AudioSettings.MasterVolume, 0.5f);
settings.Reset(AudioSettings.MasterVolume); // remove saved override; use default
```

Reads without an active provider return the definition default, except replicated `ServerSession` values on network clients. Writes need an active provider and must pass scope and value checks; rejected writes return `false`. Float and integer values clamp to authored bounds. Non-finite floats and invalid choice strings are rejected. Text values truncate to their authored maximum length.

Observe changes and dispose the returned subscription with its consumer:

```csharp
using System;
using UnityEngine;
using UnscriptedEngine.Settings;

public sealed class AudioConsumer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    private IDisposable subscription;

    private void OnEnable()
    {
        subscription = SettingsRuntime.Service.Observe(
            AudioSettings.MasterVolume,
            value => audioSource.volume = value);
    }

    private void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
```

Set `emitCurrent: false` to receive changes only. Equal effective values do not notify. Service calls must run on Unity's main thread.

`LocalUser` settings save to `Application.persistentDataPath/Settings.v2.json`. Runtime saves occur periodically while preferences are dirty, on pause, on quit, and when the settings panel closes. Call `SettingsRuntime.Save()` for an explicit checkpoint. `ServerSession` values are session state, not local preferences.

## Extending

Add more members to an existing catalog or create another catalog and provider. Public nested catalog classes are discovered recursively. No assembly-wide scan runs. Available factories:

- `Setting.Bool` for `bool`.
- `Setting.Float` and `Setting.Int` for bounded numbers.
- `Setting.Text` for limited-length strings.
- `Setting.Choice` for fixed string choices.

Factories accept optional `description`, `scope`, `editorHint`, `advanced`, and `order` metadata. Group paths use `/`, such as `"Gameplay/Camera"`. C# nesting and UI grouping are independent.

For project-specific widgets, map a setting ID, editor hint, or value kind in `SettingsTheme`, then implement a `SettingsWidget`. The generated panel provides built-in controls, group collapse, search, and an advanced-settings filter. Theme match priority is exact setting ID, editor hint, then value kind.

For a new value type, derive an immutable definition from `Setting<T>` and provide a valid default, normalization, validation, and serialization compatible with the value. Use explicit service injection or adapters if the application needs multiple profiles or service instances; the supplied providers and UI use `SettingsRuntime.Service`.

## Multiplayer

For Netcode for GameObjects, put `SettingsNetworkBridge` on a server-owned spawned `NetworkObject` that lives for the session. Assign catalog providers covering the complete server schema on every peer; disabled providers can supply schema without activating scene settings. The bridge sends current `ServerSession` values to joining clients and replicates later changes.

Clients cannot write `ServerSession` settings, and the bridge has no client-to-server write request. Initial values arrive individually; there is no snapshot-ready barrier. Gate gameplay in the connection flow if clients must wait for all values. Enforce important rules on the server. Matching setting IDs and definitions must exist on every peer.

## Limitations

- Definition references do not register settings; providers control activation, UI visibility, and write eligibility.
- IDs must remain stable. Renaming one requires migrating saved preferences; choice values are stored as strings, not indices.
- Built-in definitions cover Boolean, bounded numeric, text, and fixed-choice values. Cross-setting dependencies, apply/cancel transactions, restart policies, and graphics rollback are not built in.
- Settings UI uses legacy uGUI controls. Localization, key binding, and larger-scale list virtualization require custom UI work.
- The basic network bridge has no batching, client-admin request flow, or snapshot-ready event. Custom transport code must authenticate server values before applying them.

## Example: server-controlled choice

Use `ServerSession` for a match rule that must come from the server:

```csharp
using UnscriptedEngine.Settings;

[SettingsCatalogRoot]
public static class MatchSettings
{
    public static readonly Setting<string> Difficulty = Setting.Choice(
        id: "match.difficulty",
        defaultValue: "normal",
        choices: new[] { "easy", "normal", "hard" },
        label: "Difficulty",
        group: "Match/Rules",
        scope: SettingScope.ServerSession);
}
```

Include this catalog in the bridge schema and activate its provider in the match scene. Server code can call `TrySet`; clients read the replicated value and cannot write it.

## Notes and changelog 📝

For project examples, migration notes, theme/widget details, and network setup, see [`Framework/Settings/README.md`](../Framework/Settings/README.md). Project catalogs such as `GeneralSettings` are examples, not framework definitions.

- **2026-10-25:** Expanded settings guide with activation model, persistence, UI extension, and Netcode behavior.
