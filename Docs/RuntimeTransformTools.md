# Runtime transform tools

`UnscriptedEngine.Transforms` supplies reusable transform handles without depending on a particular item, player input package or UI theme.

- `TransformSelection` owns non-overlapping roots and cached geometry for a combined-bounds pivot. `Set`, `Toggle` and `Prune` update the selection; a parent and its child never appear together.
- `TransformGroupEdit` captures a baseline and pauses descendant rigidbodies. `Move`, `Rotate` and `Scale` apply deltas relative to that baseline. `Finish(false)` commits locally; `Finish(true)` and `Dispose` restore starting poses. Both release the paused physics state. Operations preserve hierarchy.
- `RuntimeTransformGizmo.Initialize(shader)` creates owned geometry/materials. `Present` supplies the camera, selection, mode, pivot, basis and hovered handle. `Pick` tests the rendered handles in screen space. The owner must destroy the gizmo GameObject on teardown. The authored `UnscriptedEngine/Runtime Transform Gizmo` shader renders its geometry without scene depth occluding the handles.
- `TransformHandleDrag` maps successive pointer positions to an existing group edit using a fixed drag plane. The center Move handle uses the camera plane; center Rotate uses the view axis; center Scale applies uniform factors. It has no input subscriptions or lifecycle of its own.
- `TransformPose` carries a world position, world rotation and local scale, validates finite/nondegenerate values, and applies a pose to the Transform and a root Rigidbody.

The caller owns authorization, target resolution, range checks, lifecycle, mode cycling and transport. For example, use a controller-owned modal screen to unlock the cursor, call `Present` before `Pick`, construct `TransformGroupEdit` and `TransformHandleDrag` on pointer-down, then update the drag while held. On release, capture the desired poses and either finish locally or restore the baseline before submitting them to an authoritative transport. Cancel if focus or authorization is lost. Never leave a group edit undisposed.

The cone/cube/line/ring geometry has no active colliders and stays on Ignore Raycast. The gizmo does not scan scene objects or choose which objects may be edited. Positive scale factors avoid singular/mirrored edits. Nonuniform scaling preserves rotation and projects onto scaled axis lengths because Unity Transform cannot represent arbitrary shear.

`SceneObjectAddress` is a separate shared utility for name/ordinal addresses of existing shared scene objects. It escapes path segments, distinguishes same-name siblings and supports inactive objects. Names and hierarchy must remain stable between encoding and lookup. Both Delete Wand scene tombstones and Transform Wand commits use this implementation.

`GameplayTagContainer.HasPath` queries an authored path and parent matches without loading a separate query asset; it uses the container's normal revision-aware cache.

The sandbox Transform Wand composes these pieces under `Assets/Prototypes/Default/Experiments/Gameplay/TransformWand`. Its targeting rules, exemption tag, network transaction policy and demo tools remain prototype-specific.
