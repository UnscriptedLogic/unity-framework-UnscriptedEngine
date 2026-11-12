# Object animation

Add `ObjectAnimBrain` and any combination of `ObjectAnimMove`, `ObjectAnimRotate`, and `ObjectAnimScale` to a visual pivot. These are local presentation MonoBehaviours, independent of Netcode, gameplay classes, UI, DOTween, and other host plugins. They use Unity curves and the framework's UpdateManager, with a subscription only while playing.

## Setup

1. Keep colliders, physics, navigation, and gameplay roots outside the animated visual hierarchy.
2. Add a brain and effect components to a visual pivot. An empty Components list finds effects on that same object only. To coordinate several child visuals, assign those effects explicitly. Duplicate references are ignored; inactive effects are skipped at Play.
3. Assign Target on each effect, or leave it empty to animate its own Transform. Move uses parent-local units; rotation applies local Euler offsets after the rest quaternion; scale multiplies the captured local scale (including nonuniform or zero scale).
4. Choose Shape: Tween follows Curve from rest to an offset, Punch uses a sine pulse with Curve evaluated from 1 to 0 as its decay envelope, and Wiggle uses a sine wave with Oscillations per cycle. Punch and Wiggle finish exactly at rest. Tween retains its curve's final value, including overshoot. Wiggle ignores Curve. Use integral oscillation counts for smooth repeating seams.
5. Set Duration in seconds and Cycles (1 = once, 0 = until stopped). Use Unscaled Time for effects that should continue during pause. Wire a UnityEvent to the brain's Play, Stop, or ResetPose methods. Completed fires once after all cycles; infinite playback never completes naturally.

Each effect exclusively writes one property on its Target. Use only one writer per target/property, including Animators and other brains. To layer multiple moves or rotations, use nested pivots. A single effect instance cannot be claimed by two brains at once. Changing Target restores and releases its old capture; call Play again to capture the new target. Disabling an effect restores and releases it; it rejoins only on the next Play.

## Playback contract

Play always restores the previous capture before capturing the current rest pose, so repeated firing does not accumulate offsets. Rest is captured on Play, not Awake, allowing prefab parenting and initialization first. Change gameplay poses after ResetPose, then Play to capture the new rest pose.

Stop freezes the current pose and cancels completion. ResetPose stops and restores captured properties. Natural completion retains the final sample so a chest lid can remain open; ResetPose closes it. Disabling the brain or its GameObject restores and cancels; enabling does not resume unless Play On Enable is selected. Zero duration samples the end and completes immediately once, even with Cycles = 0. Cycles and Duration are snapshotted at Play; edits take effect on the next Play. Repeated Tween cycles restart from the same rest pose, so use matching curve endpoints or a polish shape for seamless loops.

`Advance(seconds)` advances the running clock deterministically for previews/tests. In Play Mode UpdateManager already advances it: do not also drive Advance from gameplay. In Edit Mode Play/Advance can preview explicitly; always ResetPose afterward. These components do not run automatically in Edit Mode.

## Recipes

| Effect | Composition | Trigger |
| --- | --- | --- |
| Chest opening | Punch Scale on a body pivot; separate Tween Rotate brain on lid hinge | Opening calls both Play methods; closing calls lid.ResetPose |
| Processing machine | Wiggle Move + Wiggle Rotate, Cycles = 0 | Processing start calls Play; finish/cancel calls ResetPose |
| Turret recoil | Punch Move + Punch Scale on barrel's rear pivot | Each shot calls Play; rapid shots safely restart |

Example: `recoil.Play();` after a successful fire action, or `processing.ResetPose();` when processing ends. Keep those subscriptions in the gameplay adapter; the package has no knowledge of chests, machines, or weapons. Effects are not replicated; gameplay decides which clients trigger presentation.

The sandbox's `Assets/Prototypes/ObjectTweening/ObjectTweenDemo.unity` provides all three recipes with buttons. See its README for verification steps. Focused PlayMode tests are in Tests/ObjectAnimation; run with the host's package testables enabled. They include real disable callbacks, dispatcher registration, and scaled/unscaled playback while paused.
