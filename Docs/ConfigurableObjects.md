# Runtime object configuration

`UnscriptedEngine.Configuration.ConfigurableObject` opts a scene instance into runtime inspection. It requires a `NetworkObject` for host-authoritative replication. Add it to the root of a configurable prefab before spawning it; register dynamically spawned prefabs with the NetworkManager normally.

An empty **Components** list discovers MonoBehaviours on that GameObject. Assign components explicitly to narrow the list or include children. References outside the marker's hierarchy, duplicate references, the marker itself, and components belonging to a nested `ConfigurableObject` are ignored. Runtime discovery is cached for the object's lifetime; finish authoring the component list before inspection or network spawning.

## Supported fields

Public serialized fields and private `[SerializeField]` fields are discovered through the component's inheritance chain. `[HideInInspector]`, `[NonSerialized]`, static and readonly fields are excluded. Unity base implementation fields are excluded. Header, tooltip, Inspector name, Range and Min attributes supply presentation and validation metadata.

| Field | Runtime control |
| --- | --- |
| `bool` | Toggle |
| `float`, `int` with `[Range]` | Slider and precise numeric input |
| Unrestricted `float`, `int` | Numeric input |
| `string` | Text input, up to 1024 characters |
| Non-flags enum with a named value | Choice control |

Unsupported fields appear in a read-only **Unsupported fields** group. Native Unity component properties, C# properties, object/asset references, flags, vectors, colors, nested objects, lists and custom UnityEditor drawers are not automatic runtime editors. Shared assets are never traversed or mutated by field discovery.

## Sessions and control reuse

`ObjectConfigurationSession` implements `ISettingsValueSource`, the same binding contract implemented by `SettingsService`. Pass a session to `GeneratedSettingsPanel.Bind` before activation, or bind an individual `SettingsWidget` directly. The panel keeps its existing `SettingsTheme`, grouping, filtering and authored row prefabs. Instance sources never register with `SettingsRuntime` and never save global preferences.

The session captures opening values for per-field Reset and keeps up to 128 successful edits for Undo. It reads current component state, including gameplay changes. Call `Refresh` at a modest rate when showing a session; the wand uses 5 Hz. Dispose the session and unbind controls when closing. A session can receive an authorization callback, such as checking that its owner still holds the configuring item.

## Fields requiring runtime work

Direct assignment works for fields read live by the component. It does not invoke UnityEditor `OnValidate` or rebuild cached runtime objects. Implement `IConfigurableFieldAdapter` on components needing validation, setters or rebuilding:

```csharp
public bool TrySetConfiguration(string fieldName, object value, out string reason)
{
    reason = "Unsupported field or value.";
    if (fieldName != nameof(speed) || value is not float nextSpeed) return false;
    speed = nextSpeed;
    RebuildMotion();
    reason = null;
    return true;
}
```

An adapter owns assignment for all exposed fields on that component. Validate before changing state, leave state unchanged when rejecting, and apply the normalized requested value deterministically. It runs on receiving peers as well as the host. Do not use configuration callbacks to perform one-time server gameplay transactions. Member IDs hash the relative component address, declaring type and field name; peers must use the same authored hierarchy and scripts.

## Multiplayer and lifetime

Offline sessions edit locally. During networking, only the server/host can write and the target must be spawned. Clients can inspect read-only. There is no client RPC accepting arbitrary reflection paths. The host revalidates each field, value and observer distance. The authoritative `NetworkList` replicates edited values, including initial state for late joiners. This replicates configuration fields; the component's resulting gameplay/physics effects still need their normal authority rules.

Replication is bounded to 128 edited fields per object and 509 UTF-8 bytes of JSON per value. Oversized network edits are rejected before mutation. This is play-session state, not a save system; prefab and asset defaults are unchanged. Reordering components/hierarchies changes the schema and requires matching peers.

## World-space modal input

`UCanvasComponent.BlocksBackgroundPointer` defaults to true. A controller-owned world-space modal can override it to false so an overlay backdrop does not intercept its world-space raycasts. Gameplay still respects the controller's modal focus lease and underlying controller screens remain non-interactable. Such screens must manage their independent Canvas's presentation and destruction with the controller lifecycle.

Regression coverage: `ObjectConfigurationTests`, the independent-source test in `SettingsWidgetTests`, and the world-modal test in `UCanvasControllerTests`.
