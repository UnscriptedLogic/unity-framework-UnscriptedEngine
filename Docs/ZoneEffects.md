# Damage and status zones

## What they do

`DamageZone` and `StatusZone` are extra components beside `ColliderZone`. Contact detection, actor-tag Any/All requirements and rejections, layer filters, controller support, and debug visuals all stay in ColliderZone.

## Create a damage zone

1. Open **Tools > UnscriptedEngine > Gameplay > Zones > Damage Zones**. The shared creator also offers Collider and Status zone types, viewport placement, and prefab-stage creation.
2. Configure the collider and actor filters. In **Value Changes**, add one or more entries and assign each target TaggedFloatValue tag, such as `Stats.Health` or `Stats.Stamina`. Entries apply in list order using one actor-value search per application.
3. Choose **Add** with a negative amount for damage, a positive amount for healing, or **Set** for an absolute value. Example: Add `-10` subtracts ten health; Set `25` makes health 25. The value's clamps still apply.
4. Disable **Ongoing** for one successful application per actor visit. Enable it for immediate application followed by repeats at **Interval** seconds. All colliders belonging to that actor must leave before a new visit begins. Re-enabling the effect component also resets its visits.

Damage zones are red. A positive sum of configured Add amounts makes them green; zero or a negative sum stays red. If any entry uses Set, the zone stays red because actual healing depends on the target value. Existing single-entry configurations migrate automatically. Successful contact still flashes green. These colors also apply to runtime outlines and translucent faces.

Actor filters inspect `GameplayTags`; Value Tag inspects tags on the value component. These are separate conditions. Missing value tags apply nothing. Every matching active value on the resolved actor is changed. Nested NetworkObjects and separate gameplay-tag owners are excluded.

## Create a poison zone

1. Open **Tools > UnscriptedEngine > Gameplay > Zones > Status Zones**.
2. Click **Create Poison Effect Asset...**, save the definition under your prototype's data folder, and assign its Value Tag to the health tag. The definition is editable inside the creator and in its asset Inspector.
3. Set poison duration, tick interval, and value change. Defaults are Add `-10`, five seconds, and one-second ticks. Poison ticks immediately on first application and continues after leaving the zone until it expires.
4. Configure the zone's separate Ongoing and Interval settings. These control how often poison is applied or refreshed while touching the zone. Reapplying the same definition refreshes duration without stacking or resetting its tick timer. Different definitions run independently.

Each status definition has a **Debug Color** used by its StatusZone. Future effect components can supply zone colors through `IZoneDebugColorProvider`; the first enabled provider on the zone wins. Contact flashes temporarily override the base color.

`StatusEffectController` is added to the actor when needed. `Remove(definition)` removes one status; `ClearEffects()` removes all. Disabling the controller clears its active statuses.

## Ownership and extension

Actor resolution prefers the nearest NetworkObject, then GameplayTags owner, attached Rigidbody, ancestor TaggedFloatValue, and finally the collider's hierarchy root. Keep each offline actor under its own root or add a GameplayTags owner to define the boundary. Multiple colliders share one application schedule per zone and actor. Contact effects keep their own timers, so repeats continue even when sleeping bodies or idle controllers stop sending Stay callbacks.

Subclass `ContactZoneEffect` and implement `TryApply(GameObject actor)` for another contact behavior. Subclass `StatusEffectDefinition` and implement `ApplyTick` for another timed status; definitions are shared assets and must not hold mutable per-actor state. Scratch value buffers belong to each active status.

All effects run offline or on the server. TaggedFloatValue enforces its existing write permissions and replicates spawned values. Clients do not request zone damage by RPC. Networked actors need their values configured on the network prefab before spawning. Status timers and visual status indicators are currently server-local; only resulting values replicate.

Timers use scaled gameplay time. Long stalls produce at most one application per frame rather than a burst of catch-up damage. A failed contact application retries at the zone interval until it succeeds. Disabling a zone stops contact applications but does not remove poison already applied. Runtime state clears between play sessions, including disabled domain reload. Browser and desktop target builds still need gameplay verification.

## Physics force zones

Add **Wind**, **Suction**, or **Knockback** through **Tools > UnscriptedEngine > Gameplay > Zones > Collider Zones**, or add the corresponding component beside an existing ColliderZone. Trigger volumes and solid collision contacts both work. Non-kinematic 3D Rigidbodies and CharacterMovementComponent players receive forces. CharacterController players do not require a Rigidbody. Filters still apply per contact; compound colliders affect their Rigidbody once. In network sessions, forces run on the server, following the existing zone authority rule.

- **Wind Zone** continuously pushes in **Local Direction**, transformed by **Source Plane** (defaults to the zone transform).
- **Suction Zone** continuously pulls toward the nearest point on that plane, from either side. It does not pull toward the plane's center. The plane passes through the source transform origin; Local Direction defines its normal.
- **Knockback Zone** applies one instant directional impulse when a Rigidbody enters. It can apply again once all accepted colliders belonging to that body leave. This works for both trigger entry and solid collision contact.

The default direction is local **+Y**, the normal of Unity's XZ Plane primitive. Rotate the source transform or change Local Direction to aim the force. The collider controls the affected volume independently of the source plane. The selected gizmo shows the source plane and its positive normal. Set the source origin to the emitting face if the volume is offset from the surface.

**Strength** is acceleration (m/s²) for wind/suction and impulse (N·s, affected by Rigidbody mass) for knockback. Characters interpret knockback as a velocity change (m/s). Shared PhysicsForceUtility resolves targets and routes character forces into the movement simulation after input acceleration, rather than applying Rigidbody forces. Server-owned forces are consumed once per movement step and replicated through the existing authoritative state/reconciliation path; remote players may see the force after a network correction. Movement locks reject forces. Continuous acceleration runs in FixedUpdate and wakes sleeping bodies. Suction can overshoot the plane; Rigidbody damping can help it settle.

**Require Line Of Sight** defaults on for wind/suction and off for knockback in the creation tool. It tests the perpendicular segment between the Rigidbody center of mass or CharacterController bounds center and the plane, ignoring triggers, the target body's or character's colliders, the zone collider, and colliders on the source transform. **Obstruction Layers** selects which solid objects block the effect. Turn the toggle off to push/pull through walls. Zero Local Direction disables force application.
