# UI Retainer

## 🎯 What it does

`UIRetainer` captures a uGUI subtree into a texture and displays it through a material. The supplied Retainer Warp shader bends or tilts the combined panel, including TextMeshPro text. Its pointer bridge uses the same UV mapping for hover, clicks, scrolling, and dragging inside that panel.

## 🛠️ How to use it

1. Reserve layer 31 for captured UI. Exclude that layer from gameplay cameras. If layer 31 is already used, choose another dedicated layer in the component.
2. Select an object under your Canvas. Choose **Tools > UnscriptedEngine > UI > Create Retainer**.
3. Add UI beneath its stretched `Content` child. Keep that child's anchors and offsets stretched to the panel. Change the parent panel's size to resize the group.
4. Enter Play mode. Adjust **Curvature** and **Perspective** on `UIRetainer`. Set curvature to zero for perspective alone.

The menu assigns the supplied material asset. For manual setup, add `UIRetainer` to a `RawImage`, assign its immediate content child, and assign `Integrations/UI/Retainer/RetainerWarp.mat`. This asset reference keeps the shader available in builds. Effects are visible during play; edit mode shows the original children.

Content, effect material, and capture layer changes take effect when the component is enabled. **Debug > Show Gizmos** displays the selected capture bounds and texture size.

Use **Resolution Scale** and **Maximum Texture Size** to control sharpness and memory. Set **Refresh Rate** to zero for a panel that updates only when requested:

```csharp
retainer.Curvature = 0.08f;
retainer.Perspective = new Vector2(0.2f, -0.1f);
retainer.RequestRefresh();
```

Refresh uses unscaled time. Animating the effect does not require recapturing unchanged content. Resizing always requests a capture.

After adding, removing, or reparenting content at runtime, call `retainer.RefreshContent()`. It updates capture layers, restores layers on removed objects, and requests a capture. Use `RequestRefresh()` when only existing content values change; it does not scan the hierarchy.

## ⚙️ How it works

During play, the component temporarily moves `Content` into its own camera canvas. A dedicated orthographic camera renders it into an ARGB32 texture with a depth/stencil buffer. The original `RawImage` displays that texture with an owned material instance. The component restores the hierarchy, original layers, material, and texture when disabled, and releases its camera and GPU resources.

The input bridge maps display coordinates into texture pixels and raycasts the capture canvas. Child controls receive mapped pointer events. Selected controls still receive keyboard and controller events through the normal EventSystem. Set initial selection explicitly; automatic directional navigation across the capture boundary is unsupported. No static resources or subscriptions survive play sessions.

## 🧩 How to extend it

Duplicate the effect shader and material for color changes, scanlines, or other effects that preserve coordinates. `_MainTex` contains the captured image. Display blending expects premultiplied RGB.

For a different geometric warp, update both the shader's output-to-source mapping and `UIRetainerWarp.MapToSource`. Otherwise visible controls and pointer hits will disagree. The built-in component writes `_Curvature` and `_Perspective` each frame; edit those values on the component.

## ⚠️ Limitations

- One active pointer per retainer. Multi-touch gestures, nested retainers, nested canvases, and dragging between separate retainers are unsupported.
- Dropdowns or popups that create nested canvases are unsupported. Custom controls must use mapped event positions rather than reading raw mouse coordinates directly. This version targets the main display.
- Content temporarily has a different parent. Cache the `Content` reference rather than searching the original hierarchy during play. Layout ownership must stay inside `Content`; parent layout groups cannot measure its captured descendants.
- Ancestor `CanvasGroup` settings still affect the displayed image and pointer filtering, but captured controls no longer inherit their interactability. Keep control-level groups inside `Content` and clear selection when an outside screen controller blocks keyboard input.
- Dynamically created content must use the dedicated capture layer or be prepared with `RefreshContent()`. Tracked objects have their layers restored on disable.
- UI outside the capture rectangle is clipped. Distortion simulates perspective on one flattened surface; it does not create depth between children.
- Source materials must preserve composited alpha when rendering into a transparent target. Custom shaders with incorrect alpha blending can produce fringes. The display supports stencil masks and hard rectangular clipping; soft parent `RectMask2D` edges are not implemented.
- Each retainer adds a camera render, texture memory, and a display draw. A 2048×2048 color target alone uses approximately 16 MiB, plus depth/stencil and driver storage. Prefer small capture areas and reduced refresh rates for browser builds. Refresh throttling does not stop child scripts or layout changes from running.
- Use a standard URP renderer that preserves target alpha. Renderer features can alter the capture. Browser and device behavior needs validation in the target build.
