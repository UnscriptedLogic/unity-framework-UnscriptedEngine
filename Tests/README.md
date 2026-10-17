# Framework tests and architecture gaps

## Running

Attachable test components live in `Tests/Runtime` in the runtime-capable `UnscriptedEngine.Tests.Support` assembly. Keep them out of the Editor test assembly: Unity rejects Editor-only MonoBehaviours even when an EditMode test enters Play Mode. The network lifecycle test authors temporary prefab assets before entering Play Mode, registers them with Netcode, and deletes its unique asset folder during teardown. Runtime-generated NetworkObjects without prefab IDs are not valid fixtures for this Netcode version.

Run `UnscriptedEngine.Tests` from Unity Test Runner's EditMode tab. This is an Editor test assembly; `DomainReloadTests` enters and exits Play Mode itself. Save open scenes first. Those tests temporarily enable DisableDomainReload, keep scene reload enabled, and restore the previous settings and scene setup afterward. Do not run these tests during gameplay.

If package tests are not discovered, add `com.unscriptedlogic.unscriptedengine` to the host project's `testables` array in Packages/manifest.json. The host must have Unity Test Framework installed.

Coverage: EventBus ordering, placement, cancellation, mutation during dispatch, duplicate subscriptions, destroyed listeners and validation; inventory atomicity, stacking, removal, splitting, singular items and notifications; tag normalization, stable IDs, hierarchy, containers, queries and lookup; repeated Play sessions and game-instance initialization, duplicates, persistence and shutdown.

Validation so far: all test sources compile against the local Unity references. Unity Test Runner execution is still required; compilation is not a passing test run. The suite now includes local-host lifecycle integration; separate host/client processes, possession, tick timing and camera arbitration still need coverage.

## Architecture gap resolution

Implemented:

- UObject/UObjectComponent tick only while spawned. Gameplay lifetime now resets on despawn as described below.
- UGameState resets its local initialization-notification guard on despawn.
- UGameMode invalidates pending registration enumerators before dropping the network manager, even if that manager is already missing. Old work exits on its next resume without touching new-session registrations.
- GameplayTag matching, hierarchy checks and container removal now use the same ID identity as container membership.
- Tag OnValidate/OnEnable/OnDestroy increments a revision; databases and containers rebuild stale caches. Tag fields remain privately serialized; reflection or unsupported runtime mutation must not bypass validation. The revision is an invalidation token, not gameplay state, and intentionally survives Play sessions.
- Inventory Initialize is idempotent and preserves preloaded contents. It no longer acts as an implicit clear operation.
- Inventory mutation during Changed or inventory EventBus notifications returns a failed OperationResult. Read-only checks remain available. The guard is released in finally, including when a subscriber throws.

## Lifecycle and notification contracts

- BeginPlay runs once per spawn, after the object's own OnSpawned hook and framework/owner readiness. EndPlay runs once on despawn or destruction. HasBegunPlay resets before EndPlay; cached game mode/state references clear afterward.
- UObjectComponent observes owner EndedPlay, including while disabled. Reassigning its owner ends the old gameplay lifetime. Disable/enable pauses gameplay and does not repeat BeginPlay; enable-time subscriptions must still be paired with disable-time cleanup.
- UObject/UObjectComponent seal OnNetworkSpawn, OnNetworkDespawn and OnDestroy. Migrate overrides to protected OnSpawned, OnDespawned and OnDestroyed. Call base when overriding another subclass's hook. Framework teardown runs in finally even if a hook throws. Do not hide Unity lifecycle methods with new methods.
- Existing framework and project consumers have been migrated. Player camera/input subscriptions, mount/race/throw cleanup, held visuals, and knockback effects have matching cleanup. External package consumers must migrate their overrides before updating.
- UGameState starts a fresh server-owned runtime state on each spawn: initialization false and connected players empty. IsNetworkReady prevents game mode from racing the state reset. Authored settings remain unchanged; persistent matches require an explicit snapshot/restore implementation.
- Inventory Initialize preserves contents. Inventory notifications isolate and log each subscriber exception; committed operations remain successful, and later subscribers receive notification. Reentrant inventory mutations are rejected until notification delivery completes.
- EventBus.PublishNotification is for post-commit notifications and rejects ICancellableEvent. EventBus.Publish still propagates exceptions and honors cancellation. Existing duplicate-subscription and dispatch-snapshot semantics remain unchanged.

## Validation and remaining limits

Production package, project scripts, and test sources compile. Tests include actual host spawn/despawn/reuse, disable/enable, fresh server state, notification exception isolation, and sealed-entry-point checks. Unity Test Runner execution remains pending; no passing runtime result is claimed. Separate host/client processes and reconnect races still need runtime validation.

Unity Awake/OnEnable/OnDisable and custom game-mode/state overrides still require correct base calls. No analyzer is installed to detect hidden Unity messages. Match snapshot/restore is intentionally not implemented. Scene reload must remain enabled when disabling domain reload.

## Central update manager

UObject, UObjectComponent, Ticker, and NavMeshMovementComponent share one persistent UpdateManager with one Unity Update and one FixedUpdate. Other standalone utilities/UI retain their own Unity callbacks. Subscriptions reset at SubsystemRegistration for domain reload disabled, with scene reload enabled.

For each eligible object, the order is Tick, ServerTick, ClientTick. Dedicated servers run shared and server hooks; clients run shared and client hooks; hosts run all three. FixedTick, ServerFixedTick, ClientFixedTick follow the same rules. Role hooks require a spawned network object. Client hooks run for all client-side instances: check IsOwner for local-player work. These callbacks use Unity frame/physics time, not NGO's network tick rate. Existing shared hooks retain their behavior.

Override TickOrder for managed callback ordering (lower first, default zero). Character movement uses -50 and NavMesh synchronization -40. Equal priorities preserve subscription order; re-enabling resubscribes. DefaultExecutionOrder on a subscriber does not order its managed callbacks relative to other subscribers. Native callbacks outside the manager retain Unity's ordering relative to the manager itself.

RegisterUpdate/RegisterFixedUpdate accept one callback per Behaviour per phase, optionally with an order. Registering the same callback/order is idempotent; a different callback replaces it. Unregister on disable/destruction; disabled owners are skipped and destroyed owners pruned defensively. Additions during dispatch wait until the next dispatch of that phase; removals take effect immediately. Exceptions are logged per subscriber and other subscribers continue. No snapshot allocation occurs during steady dispatch. UObject role hooks stop after lifecycle invalidation during a shared hook.

UObjectComponent now observes owner readiness instead of polling every frame. Reparenting refreshes ownership; call RefreshOwner after adding an owner component dynamically without reparenting. Lifecycle overrides must call base. Do not add Unity Update/FixedUpdate methods to managed subclasses; override the tick hooks.

Tests cover subscription mutation, priorities, enable state, destruction, exception isolation, channel independence, session reset, host/dedicated-server role routing, and despawn/respawn. A separate remote-client integration run and profiler comparison remain outstanding. Centralization reduces Unity message entry points; actual frame-time savings depend on workload and have not been measured.

## Reusable extraction and integration boundaries

Framework now includes NormalizedValueControl, LeverControl, ValveControl, GaugeDisplay, SignalEmitter, InputFocus, PlayerInputEvent and semantic movement/look events, PawnViewRig, CameraRelativeMovement, EquipmentSelection, MovementAnimator, IMotionSource, MotionCameraEffect, BodyLeanEffect and JsonSettingsStore. Lever/valve state and remapping share one base; visual animation uses UpdateManager and has no DOTween dependency. Body lean retains LateUpdate because it applies cosmetic rotation after movement.

Existing project scripts remain compatibility adapters where possible, retaining their original .meta GUIDs and serialized field names. Existing prefab fields are inherited by the new implementations. Verify prefab overrides and persistent UnityEvent callbacks in Unity before deleting adapters. DefaultPlayerPawn remains the game composition/input layer; its reusable camera, movement and equipment algorithms are package services. Key-specific events and game rules stay in the project.

Core assembly references only NGO and Cinemachine. Required Unity engine modules are declared in package.json. Integrations/UI is a separate assembly enabled for com.unity.ugui >= 2.0.0 (TMP included); Integrations/InputSystem is enabled for com.unity.inputsystem >= 1.0.0. Consumers install these optional packages only when using those integrations. Core has no Quantum Console, DOTween, URP, or project-script dependency. Cinemachine itself brings its declared Splines dependencies. The Control Binding sample is opt-in through Package Manager.

InventoryView observes IInventory.Changed, supports rebinding/capacity changes, and owns no cursor or gameplay input. Project UI_InventoryPage acquires an InputFocus lease. Settings and console use the same service; closing one modal leaves other leases active. Dispose leases on disable; destroyed owners are pruned when IsBlocked is queried. Cursor is process-wide; an optional controller argument scopes input blocking, not separate hardware cursors. InputFocus resets at SubsystemRegistration. Project input publishers neutralize blocked gameplay actions; movement and throwing also check focus to clear held state.

InputActionSession clones action assets per controller and disposes them on disable/despawn. PlayerActionNames keeps existing default action names while exposing their mapping. Device pairing/split-screen routing is still a project responsibility. PawnViewRig expects a body, camera anchor and first-person/third-person/aim camera rig; game code supplies these dependencies and body-rotation policy.

JsonSettingsStore loads legacy raw JSON and writes a versioned envelope using a temporary file and atomic replacement. Unsupported versions fail explicitly; game code restores defaults. The current project validates settings values. Future schema migrations and platform-specific storage backends remain application policy; filesystem replacement support must be verified on target platforms.

Validation: core compiled with only engine/BCL, NGO and Cinemachine references, also with UNITY_EDITOR removed; optional integrations, project scripts, runtime test support and Editor tests compiled. ReusableServicesTests covers overlapping focus leases, destroyed owners, legacy/versioned settings replacement and control transitions. A clean consumer Unity project was prepared under Temp/UnscriptedConsumer, but batch execution stopped with exit code 198: "No valid Unity Editor license found." No Unity test pass or prefab migration pass is claimed.

Remaining project systems: authoritative mounting/seat integration, networked inventory throwing, AppleCat-specific AI steering, and race/spline-distance rules were not promoted into core. These need separate behavioral refactors, not file relocation. Run EditMode tests, host/client gameplay, stacked panels, camera handoffs, and prefab serialization checks in a licensed Editor before release.

## Behavioral test audit fixes

- Game-instance persistence unloads its actual creation scene, not an unrelated scene.
- Tag deserialization uses JsonUtility.FromJsonOverwrite to replace serialized contents on a warmed container and checks removal of stale exact/ancestor entries. Query cases independently exercise required, any and blocked clauses.
- Inventory reentrancy must execute its nested attempt exactly once before checking rejection.
- Settings readers consume handwritten legacy/versioned fixtures. Writer output is inspected through a separate test DTO. Internal write/replace delegates inject partial-write and replacement failures; tests require the original file to remain unchanged and temporary files to be removed. This validates failure handling, not power-loss durability.
- Tick probes record ordered shared/server/client traces and frame/physics-step identities. Tests require positive component activity, reject duplicate dispatch within a step, and cover a real loopback client connected to a separate in-process server. ForceTick bypasses only game-mode readiness for that client-role test; NGO role state and update dispatch remain real. Full remote-process startup/reconnect coverage remains separate.
- Play-session tests now check actual modal cursor state and assigned valve-handle animation/completion. Focus service coverage does not establish project-specific input binding or every UI flow.
- Removed session-reset assertions for singleton/ticker systems that the fixture never populated. Lifecycle sealing remains explicitly an architecture contract test, not behavioral proof.

These revisions compile against local Unity references. Runtime execution and mutation-test execution remain unverified; the previously attempted clean-consumer batch run was blocked by the Unity license. Suggested mutation checks: remove DontDestroyOnLoad; empty OnAfterDeserialize; omit required-tag checking; skip Changed delivery; duplicate/reorder tick dispatch; write directly over the destination file. Each corresponding behavioral test should fail independently.
