# Damage and status zones

## What they do

`DamageZone` and `StatusZone` are extra components beside `ColliderZone`. Contact detection, actor-tag Any/All requirements and rejections, layer filters, controller support, and debug visuals all stay in ColliderZone.

## Create a damage zone

1. Open **Tools > UnscriptedEngine > Gameplay > Damage Zones**. The shared creator also offers Collider and Status zone types, viewport placement, and prefab-stage creation.
2. Configure the collider and actor filters. In **Value Changes**, add one or more entries and assign each target TaggedFloatValue tag, such as `Stats.Health` or `Stats.Stamina`. Entries apply in list order using one actor-value search per application.
3. Choose **Add** with a negative amount for damage, a positive amount for healing, or **Set** for an absolute value. Example: Add `-10` subtracts ten health; Set `25` makes health 25. The value's clamps still apply.
4. Disable **Ongoing** for one successful application per actor visit. Enable it for immediate application followed by repeats at **Interval** seconds. All colliders belonging to that actor must leave before a new visit begins. Re-enabling the effect component also resets its visits.

Damage zones are red. A positive sum of configured Add amounts makes them green; zero or a negative sum stays red. If any entry uses Set, the zone stays red because actual healing depends on the target value. Existing single-entry configurations migrate automatically. Successful contact still flashes green. These colors also apply to runtime outlines and translucent faces.

Actor filters inspect `GameplayTags`; Value Tag inspects tags on the value component. These are separate conditions. Missing value tags apply nothing. Every matching active value on the resolved actor is changed. Nested NetworkObjects and separate gameplay-tag owners are excluded.

## Create a poison zone

1. Open **Tools > UnscriptedEngine > Gameplay > Status Zones**.
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
