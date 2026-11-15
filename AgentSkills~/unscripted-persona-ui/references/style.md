# Style and evidence

## Visual grammar

Personal minimal Persona-esque: high contrast, asymmetry, deliberate diagonals, restrained decoration, compact purposeful controls. Broad near-black/charcoal planes and white text carry the hierarchy; one saturated accent organizes the screen. Orange/gold appears in the portfolio and Index; purple is the Momentum Mayhem setting. Preserve context-specific color rather than forcing every screen orange. Semantic colors may still distinguish progression or categories.

Use offset or slanted title ribbons, hard-edged panels, thin inset borders, and occasional diagonal full-bleed bars. Keep large content regions simple. Background patterns are subordinate: oversized dark repeated lettering, low-contrast checkerboard, or dimmed gameplay. Avoid multiplying competing angles. Decorative shapes may bleed off screen; content and controls stay within safe bounds.

### Layered backing recipe

The user's corrected header in [layered-header.png](layered-header.png) defines the preferred backing accent. A dark foreground shape sits over a slightly larger saturated copy of its backdrop. The colored layer has its own angle and a small offset. It peeks out unevenly at selected edges and corners, giving the composition a cut-paper appearance.

- Keep an upright parent for layout and hit areas. Arrange siblings in draw order: `Accent`, foreground `Ribbon`/surface, then text/content.
- Reuse the foreground's base silhouette or image for the accent. Enlarge it slightly with rect margins or scale, then adjust its rotation and position independently. Tune the exposed edges against the reference; do not enlarge the whole widget or create a thick uniform outline.
- Keep the foreground dark and text readable. The accent can show as a broad wedge at one corner and a fine sliver along another edge. A thin underline alone does not reproduce this backing treatment.
- Use the existing Corner Warp material when following the corrected header's quadrilateral shape. In Unity, inspect `Integrations/UI/Shaders/CornerWarp.shader` and `UnscriptedEngine_UI_Corner Warp.mat`. The current header uses sprite-free Images with this shared material and separate Image tints. Preserve that setup rather than restoring the old slanted sprite. Reuse a material for the same shape; a different corner configuration needs its own deliberate material/shape rather than mutating every shared instance.
- The title may have its own restrained tilt, as in the reference. Keep paragraphs and control hit areas upright. Do not apply one global angle to every layer or reuse the header's numeric offsets without checking the destination size.

Apply the treatment where an accent backing serves the hierarchy, especially title blocks and prominent surfaces. Thin separators, progress tracks, and semantic markers can retain their own simpler geometry. Preserve a separately requested UI style.

The current `Persona_Header.prefab` is the concrete Unity reference. Its edited appearance takes precedence over older generated examples. `PersonaLayeredStyle.Apply` supplies the shared authoring recipe when generating new pieces; its existing-prefab update preserves the authored header. Reuse that prefab and recipe rather than reintroducing the old thin-underline default. If the user revises the reference again, align the recipe with the new source before regeneration.

Use bold condensed, readable display text for game headings and compact labels. Portfolio headings can use a more angular italic display face; body copy uses a clean readable face. Exact font identities are unverified: inspect project TMP assets rather than inventing a font attribution. White text with a dark edge/shadow can preserve legibility over moving backgrounds. Do not skew paragraphs or distort icons with their containers.

## Composition recipes

- **Portfolio master/detail:** narrow left project thumbnail rail, much larger right preview/detail area, angled title at upper left, thin accent rail and cropped diagonal bands. Selection updates details in place and retains navigation context. The empty black preview in one still is a transient/loading capture, not a requirement to leave the design empty.
- **Season Overview:** broad horizontal white progress strip across dimmed/blurred gameplay, dark framing, title overlapping upper edge, narrow gold underline. Diamond milestones and small intermediate diamonds make stage grouping readable; labels distinguish qualifiers and finale. Completed/current/locked states must remain distinct beyond color. Single centered Back control at bottom.
- **The Index:** two large side-by-side charcoal panels: categorized icon collection at left, selection details at right. A gold title ribbon overlaps the left panel; thin inset framing adds structure without filling the background. Tier dividers use small category colors. Keep collection scrolling independent from stable details and bottom Close. Empty state explains the selection action.
- **Momentum Mayhem menu:** compact vertical action list on an angled dark slab with a large gameplay preview retained alongside it; secondary share/code pane opens beside the menu. Primary action receives stronger size/emphasis. Keep navigation shallow and preserve the preview context.

## Motion grammar

Observed in recordings: full-screen colored wipes with a dark title strip between portfolio sections; diagonal menu slabs revealing adjacent content; switching left-side selection updates the large preview/details. Menu views can letterbox the composition. Use layered entrance/exit sequencing so framing establishes the destination, then navigation and content resolve.

Use quick decisive focus feedback and directional panel travel. Keep headings, frames, and content related but capable of slight stagger. Exit should clear the previous state before the replacement is actionable. Avoid continuous bounce, excessive overshoot, or unrelated motion on every element. Selected state persists after hover leaves; disabled state remains readable. Reduced motion should retain state changes through brief fades or immediate changes where the host supports it.

Initial tuning suggestions, **not measured source timings**: focus 80–140 ms; panel entrance 180–320 ms; stagger 25–60 ms; full section wipe 300–500 ms. Tune against bundled recordings before claiming a match. Determine easing from motion preview; do not attribute an exact curve to the source without measuring it.

## Evidence files

Images: `portfolio.png`, `season-overview.png`, `index.png`, and `layered-header.png` beside this document. The layered header is the user's explicit correction for accent construction and takes precedence on that detail. Recordings: `portfolio.mp4`, `momentum-mayhem.mp4`, `ui-designer.mp4`. These are copied user references; retain them with the skill so temporary filesystem paths are unnecessary.

External sources supplied by the user:

- https://www.unscriptedlogic.com/
- https://www.unscriptedlogic.com/uidesigner
- https://github.com/UnscriptedLogic/react-website-UnscriptedLogic
- https://github.com/UnscriptedLogic/unity-game-slowpacedfightinggame
- https://github.com/UnscriptedLogic/unity-game-TIM

Website and repository fetches failed during initial authoring (2026-11-13). They have **not** been inspected for asset paths, exact fonts, animation parameters, or APIs. If reachable in a future task, inspect their actual assets and implementations before reusing them. Do not fabricate repository findings.

Local API inventory: package `Integrations/UI/UIAnimReactive.cs`, `UIAnimReactive_Hide.cs`, and `UIAnimReactive_Shake.cs` provide value-threshold reactive effects. `Framework/Animation/ObjectAnimBrain` provides object animation separately. The user authorized general UIAnims components at host-project-relative `Assets/Prototypes/Default/Experiments/Presentation/UIAnims/`: `UIAnim` provides move/scale/alpha transitions and `UIAnimGroup` provides stagger/completion/input gating. Read their real source and README. `Assets/Prototypes/Default/Experiments/Presentation/PersonaUIKit/TransitionDemo` assembles the three screen examples through UCanvasController. Host prototype scripts must be included separately when carrying the package to another project.
