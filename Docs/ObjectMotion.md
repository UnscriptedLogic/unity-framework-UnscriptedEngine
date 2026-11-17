# Object motion components

Attach `ObjectTranslaterComponent`, `ObjectScalerComponent`, or `ObjectRotaterComponent` to the object to animate. Each is standalone; no brain, Animator, or tween plugin is needed. Use one writer per property. These components do not replicate motion or attach riders to moving platforms.

- **Play On Awake** starts once in Start, after Awake initialization. Otherwise connect a UnityEvent to `Play()`.
- **Points** are offsets from the captured starting pose. Translation uses world-axis offsets, rotation uses Euler offsets relative to the starting rotation, and scale uses additive local-scale offsets. Origin is implicit; do not add it as the first point unless intended.
- **Traversal / Path** goes origin → A → B → C. **Return To Origin** goes origin → A → origin → B → origin → C → origin.
- **Completion / Stop** retains the final pose and fires Completed once. **Restart** jumps to origin and repeats. **Ping Pong** reverses the entire route, including its origin, repeatedly. Loop modes do not fire Completed.
- **Speed** is translation units/second, rotation degrees/second, or scale units/second before axis weighting. Scale and full Euler rotation share a segment duration across axes. Full-angle rotation measures the length of the Euler delta vector; shortest rotation measures the quaternion angle.
- **Motion Mode / Transform Only** ticks in Update. **Physics Kinematic** requires a kinematic Rigidbody on the same GameObject and ticks in FixedUpdate. Translation/rotation use MovePosition/MoveRotation. Scaling still writes localScale because Unity has no Rigidbody scale operation; changing collider scale is not a swept physics move.

`Play()` restarts from the original captured pose without accumulating offsets. `Stop()` freezes and cancels completion. `ResetPose()` stops and restores the origin. `CaptureCurrentPose()` stops and makes the current pose the new origin. Disable stops and retains the current pose; enable does not resume automatically. Origin is captured on the first Play, or explicitly through CaptureCurrentPose. Points, mode, traversal, and completion are copied when Play starts; leave other settings unchanged during playback. Speed may be changed while playing.

## Scale

Use **Axis Scaling** `(1, 0, 1)` to lock Y or `(1, 0.5, 1)` to scale Y at half the rate and displacement. With a starting scale of `(1,1,1)`, point `(2,2,2)` and weights `(1,0.5,0)` finish at `(3,2,1)`. **Maximum Threshold** clamps absolute local scale per axis. It never clamps below the starting magnitude, so ResetPose always restores the captured pose. Negative offsets allow shrinking, including crossing zero if explicitly configured. Use Speed as the scaling speed.

## Rotation

**Shortest** uses quaternion interpolation along the shortest arc. **Full Angles** interpolates authored Euler angles: `(0,720,0)` performs two complete turns. Use enough physics steps for the desired angular speed; endpoints alone cannot encode multiple revolutions in one physics step.

## Moving platform example

Add a kinematic Rigidbody and ObjectTranslaterComponent, choose Physics Kinematic, set Points to `(0,3,0)` and `(5,3,0)`, Speed to 2, Completion to PingPong, and enable Play On Awake. This follows origin → up → across → up → origin. Rider support belongs to the character controller.

Tests in Tests/ObjectAnimation/ObjectMotionTests.cs cover sequencing, replay, return paths, looping, full rotations, scale weighting and limits. Advance(seconds) allows deterministic tests; runtime already drives it through UpdateManager, so do not also call it from gameplay.
