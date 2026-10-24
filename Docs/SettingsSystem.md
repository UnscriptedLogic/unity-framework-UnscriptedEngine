# Code-defined settings

Settings definitions live in C# catalogs. Scene-local `SettingsProvider` components activate them for the lifetime of their scene. `SettingsRuntime.Service` owns effective values, validation, change notifications, and local preferences.

## Declare a catalog

Use typed, immutable `Setting<T>` declarations. IDs stay stable across releases so saved preferences remain attached to the same setting.

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

Create a provider that selects the catalog:

```csharp
using System;
using UnscriptedEngine.Settings;

public sealed class AudioSettingsProvider : SettingsProvider
{
    protected override Type CatalogType => typeof(AudioSettings);
}
```

Add provider to a scene-local Settings Container. Providers register on enable and unregister on disable or destruction. Nested public catalogs are discovered recursively. The editor linker processor preserves types marked with `SettingsCatalogRoot` in player builds.

## Read and write values

```csharp
SettingsService settings = SettingsRuntime.Service;
float volume = settings.Get(AudioSettings.MasterVolume);
bool changed = settings.TrySet(AudioSettings.MasterVolume, 0.5f);
settings.Reset(AudioSettings.MasterVolume);
```

Reads without an active provider return the definition default. Writes require an active registration and valid value. Observe changes with `SettingsService.Observe`; dispose the returned lease when the consumer stops listening. Local-user overrides save to `Application.persistentDataPath/Settings.v2.json`.

## UI and network

`GeneratedSettingsPanel` displays definitions from active providers. Its default template supports search, advanced settings, collapsible groups, built-in controls, and custom widgets through `SettingsTheme`.

For Netcode for GameObjects, place one `SettingsNetworkBridge` on a persistent spawned `NetworkObject`. Assign matching providers as its schema; these references do not activate their settings. `ServerSession` values replicate from server and remain read-only on clients.

See [`Framework/Settings/README.md`](../Framework/Settings/README.md) for setup, migration, persistence, customization, networking, and project examples.
