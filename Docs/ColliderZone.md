# Collider Zone

## What it does

`ColliderZone` turns 3D trigger or collision contacts into filtered `UnityEvent<Collider>` callbacks. Gameplay stays outside the zone: wire a door, checkpoint, audio cue, or another component to its events.

## How to use it

1. Open **Tools > UnscriptedEngine > Gameplay > Collider Zones**.
2. Choose Box, Sphere, or Capsule; configure dimensions, filters, and events; click **Create Collider Zone**. The open prefab stage takes precedence over the scene. A selected object in that stage becomes the parent.
3. Add `GameplayTags` to the player or its collider's parent when using tag filters. These are framework GameplayTag assets, not Unity's single GameObject tag.
4. Wire Entered, Stayed, and Exited in the Inspector. Dynamic listeners receive the contacting collider. Code can use `zone.Entered.AddListener(OnEntered)` and remove the same listener during cleanup.

The default filter accepts every layer and requires no tags. Require Any needs one match; Require All needs every match. Reject Any rejects one match; Reject All rejects only the full combination. Empty lists disable their condition. All four conditions combine, with rejection taking precedence. A child tag also matches its authored parent tags. The nearest `GameplayTags` owner is used; owners are not merged.

## How it works

Use one collider on each zone GameObject. `isTrigger` selects trigger or collision behavior. Each contacting collider has its own Enter/Stay/Exit lifecycle, so a character with several colliders can produce several entries. Layers are read from the contacting collider, not its parent.

Physics still controls callbacks: the layer collision matrix must allow contact. Triggers need a Rigidbody on at least one side. The creator defaults to a kinematic Rigidbody; ordinary Rigidbody collision callbacks require a non-kinematic body on one side. Framework `CharacterMovementComponent` also forwards `OnControllerColliderHit` into solid zones automatically. Other CharacterController movers must call `ColliderZone.ForwardControllerHit(hit)` from that callback. Forwarded events receive the contacting CharacterController; Enter fires once and Stay follows subsequent movement hits. The zone retains idle controller contact within its skin tolerance and releases it when the controller moves away. Stayed follows Unity's physics callbacks, including sleeping-body behavior. A filter change can admit a contact on a later Stay callback. Disabled, destroyed, or newly rejected contacts are removed during FixedUpdate; destroyed collider event arguments compare null. Disabling the zone clears state without emitting Exited events.

## Debugging

Scene view outlines remain visible without selecting the zone while the editor Gizmos toggle is enabled. Enable **Show Gizmos** and press **F3** for runtime wireframes and translucent faces and a looked-at zone readout: mode, active contact count, accepted entries, last contact, and layer mask. Blue means trigger; orange means collision; accepted entry flashes solid green for the configured duration, using unscaled time.

Settings under **Debug > Zones** toggle nearby outlines and adjust face opacity (12% by default; zero hides faces). **Debug > Entities** supplies the shared distance and display limit. Looking at a zone keeps its outline visible when nearby outlines are off. Runtime outlines and faces are batched into one mesh with two draw calls and refresh at 20 Hz, capped by the display limit. Box, Sphere, and Capsule shapes use shape outlines; other colliders use their world bounds. The runtime view requires the existing DebugUI integration and its package dependencies.

## Extending and limits

Compose additional behaviors through the events. `ColliderZoneFilter` and `ColliderZoneWireframe` can also be used independently. Use separate child zones for compound shapes. This version handles 3D physics only. Runtime lines use depth testing; geometry can hide them. Static zone registration and contact state reset between play sessions, including when domain reload is disabled. Browser and desktop builds share the same gameplay code; target-build rendering still needs verification.
