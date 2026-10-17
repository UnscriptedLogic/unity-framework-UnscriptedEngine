# Unreleased

- Add LogService with source type/context, severity methods, category filtering, a global switch, per-call player-build opt-in, and play-session reset.
- Add typed setting definitions, nested groups, reference-counted scene modules, validated runtime values, subscriptions, and local JSON preferences.
- Add generated uGUI settings with search, advanced filtering, collapsible groups, prefab themes, and custom widget hooks.
- Add optional server-authoritative Netcode settings synchronization and client read-only enforcement.
- Document setup, typed catalogs, prototype additions, customization, persistence, networking, and limitations in Framework/Settings/README.md.

# V0.2.1

- Add modular Editor generators for framework scenes and linked controller/pawn prefabs, with optional scene wiring and extensible player templates.
- Generate acronym-prefixed scripts in a Scripts subfolder and scenes in a Scenes subfolder, creating missing folders automatically.
- Validate names and output collisions; resume asset generation after script compilation.
- Runtime and Editor source compilation passed. Unity test execution remains blocked by the missing batch Editor license.

# V0.2.0

- Breaking: UObject/UObjectComponent now own sealed OnNetworkSpawn, OnNetworkDespawn and OnDestroy entry points. Derived classes use OnSpawned, OnDespawned and OnDestroyed hooks instead. Existing framework and project consumers have been migrated.
- BeginPlay/EndPlay now pair per network spawn; disable/enable pauses without restarting gameplay. Owner shutdown ends component gameplay, and destruction is an idempotent fallback.
- Reset server game-state readiness and connected players on every spawn; wait for state initialization before starting game mode.
- Add PublishNotification/Notify APIs that log individual subscriber failures and continue delivery. Inventory uses these post-commit notifications. Regular Publish retains exception and cancellation semantics.

- Gate gameplay ticks on network spawn state and reset the game-state notification guard on despawn.
- Invalidate stale pending player registrations when detaching a network manager.
- Use tag IDs consistently for matching and removal; invalidate cached tag lookups when authored tags change.
- Preserve inventory contents on repeated Initialize calls and reject reentrant mutations during change notifications.

- Reset framework singletons and static event subscriptions on each play session, supporting disabled domain reload with scene reload enabled.
- Preserve game mode/state initialization subscribers when an instance is destroyed within the same session. Subscribers must unsubscribe when their own lifetime ends.
- Add an Editor regression test covering successive Play Mode sessions without domain reload.
- Keep scene reload enabled. Disabling both reloads is not supported by this change. Custom static state in game code still needs its own session reset; subscribe during Awake/OnEnable or later, not only in static constructors.

### Central gameplay update dispatch
- Added a persistent, domain-reload-safe UpdateManager with mutation-safe frame/fixed subscriptions and explicit execution order.
- Migrated UObject, UObjectComponent, Ticker, and NavMeshMovementComponent to managed callbacks.
- Kept shared Tick/FixedTick; added server/client hooks. Hosts run both roles, dedicated servers only server, clients only client.
- Replaced component owner-readiness polling with lifecycle notifications and RefreshOwner.
- Added dispatcher and network-role regression coverage. See Tests/README.md for migration contracts and validation limits.

### Reusable project components
- Extracted controls, inventory views, settings storage, motion presentation, camera/look handling, movement steering and equipment selection behind project compatibility adapters.
- Added shared modal input/cursor leases, semantic input contracts and isolated Input System sessions.
- Removed unused core assembly references; UI and Input System integrations have independent assembly boundaries. Added an opt-in control-binding sample.
- Added focused regression tests. Source compilation passed; clean-consumer Unity test execution was blocked by missing batch Editor license.

### Regression test quality
- Validate observable lifecycle, dispatch order, serialization compatibility, persistence, and notification behavior with independent expectations.
- Cover failed settings writes/replacements without losing saved data; strengthen client-only routing, modal cursor, and control animation coverage.

# V0.1.6

- added local-only button interaction UnityEvent support
- prevented button RPC calls when its NetworkObject is not spawned

# V0.0.1

- converted project into package for distribution
- created Editor scripts and Tick System for more utility

#V0.0.2

- updated UI binding for better readability by converting the parameter type from 'object' to generic
- shifted the Bindable class to the UObject so that any UnscriptedEngine class can use it

#V0.0.3

- updated a few small bugs. Improved overall CanvasController and UI Binding flow

#V0.0.4

- added RandomLogic and SimpleBuildHandler from UnscriptedLogicLib.dll
- added USliderComponent for sliders

#V0.0.5

- added OnPossess and OnUnPossess for pawns

# V0.1.2

- added a persistent UGameInstance lifecycle for application-wide state
- added optional target rotation following to camera magnets
