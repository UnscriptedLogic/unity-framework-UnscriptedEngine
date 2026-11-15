# Personal Persona UI prefab kit

All assets live in `Integrations/UI/Styles/Persona/`. Prefabs are in `Prefabs/` and use the `Persona_` prefix. They are optional style assets, not a global UI default. The kit travels with the package; the authoring prototype is not a runtime dependency.

Requires the package's uGUI integration and TMP Essential Resources, including the standard TMP shaders. The kit bundles its own font atlas and sprites; it uses the normal TMP shader rather than maintaining a duplicate shader implementation.

Open `Persona_Gallery.prefab` to see the collection. `PersonaKitPreview.png` is a rendered overview. The gallery owns a standalone preview Canvas; individual pieces do not own canvases or event systems.

## Components

| Prefab | Purpose | Editable content and wiring |
| --- | --- | --- |
| `Persona_Header` | Dark Corner Warp ribbon, independently tilted title, slightly larger rotated gold backing | `Title`, `Ribbon`, `Accent`; 560 x 92 reference size; preserve the edited layering and offsets |
| `Persona_Subheader` | Compact section heading with dark face and offset gold backing | `Title`, `Backdrop`, `Accent`; 440 x 44 |
| `Persona_Button` | Angled text action | Root `Button.onClick`; `Label`; visual `Background` and `Accent`; 248 x 52 |
| `Persona_IconButton` | Compact square action | Root `Button.onClick`; replace `Label` glyph or supply a decorative icon child; 56 x 52 |
| `Persona_ListRow` | Selectable two-column entry with a layered selected backing | Root `Toggle`; `Label`, `Value`, `SelectionMarker`; 560 x 56 |
| `Persona_ItemSlot` | Square selectable inventory cell; variant of ListRow | Root `Toggle`; `IconPlaceholder` or your icon child, `Value` for quantity, layered gold selection backing; 88 x 88 |
| `Persona_ListTable` | Scrollable list with column labels and single selection | `ColumnHeaders`, `Viewport/Content`, `Scrollbar`; root `ScrollRect`; six sample nested rows; 560 x 320 |
| `Persona_Panel` | Bordered dark surface with nested header and an empty content slot | `Persona_Header/Title`, `Content`, `Background`, `Frame`; 640 x 440 |
| `Persona_Divider` | Fine rule with short gold lead | `Line`, `Accent`; 440 x 20 |
| `Persona_Badge` | Small dark status ribbon with offset gold backing | `Label`, `Ribbon`, `Accent`; 128 x 30 |
| `Persona_Tab` | Persistent selected tab | Root `Toggle`; `Label`, `SelectedUnderline`; 180 x 48 |
| `Persona_Toggle` | Labeled on/off control with diamond check | Root `Toggle`; `Label`, `Box/Check`; 320 x 48 |
| `Persona_TextInput` | Single-line TMP input | Root `TMP_InputField`; `TextArea/Text`, `TextArea/Placeholder`; 360 x 54 |
| `Persona_ProgressBar` | Display-only normalized progress | Root `Slider.value` from 0 to 1; `Track`, `Fill`; 360 x 20 |
| `Persona_Modal` | Full-screen dim backdrop with centered panel and two actions | `Persona_Panel/Content/Message`, `Persona_Panel/Content/Actions`; buttons are intentionally unbound; 1600 x 900 reference size |
| `Persona_Gallery` | Standalone visual overview built from nested pieces | Preview only; not an application screen controller |

## Assembly and behavior

Place pieces beneath a `UCanvasComponent` screen owned by `UCanvasController`, or beneath your existing compatible uGUI canvas. Controls include the existing `UWidgetComponent`, which registers with the nearest screen. Plain headings and surfaces have no focus behavior. Use one EventSystem with the project's input module.

Keep roots upright for layout and input. Decorative `Background`, ribbons, markers, and accents can be targeted independently. Use anchors, root sizes, and layout elements to fit the destination; reference sizes are starting points, not an automatic responsive layout system. Avoid shrinking the entire kit to fit narrow viewports. Stack list/detail panels vertically when needed.

Buttons use standard uGUI normal, hover, pressed, selected, and disabled tint states with an immediate transition. Rows and tabs use a larger, offset `Toggle` backing for persistent selection. `SelectionMarker` and `SelectedUnderline` retain their existing object names for binding compatibility, but now display backing shapes. Assign related tabs/rows to a `ToggleGroup`; the table already does this. A row's selection is separate from its value text: the consumer updates labels or detail content after selection. Demo labels do not create application logic.

For a disabled control, set `interactable = false`. Apply a lower CanvasGroup alpha to the entire piece if you need the visibly muted treatment shown in the gallery. Restore alpha when enabling it. The progress bar is intentionally non-interactive and excluded from navigation; use the existing Slider as the value owner, not a separate custom update loop.

The list table uses a `VerticalLayoutGroup` and `ContentSizeFitter` for small lists. Remove or replace its six sample rows for real data. Instantiate `Persona_ListRow` under `Viewport/Content`, assign its Toggle to that content's ToggleGroup, and bind labels and selection in the calling screen or presenter. For very large collections, use the project's virtualization/pooling solution rather than growing the layout indefinitely. ScrollRect does not automatically scroll to keyboard focus; implement that behavior in the owning list presenter if required.

The modal is an artwork composition. Put it under a managed modal screen; set `CanvasLayer.Modal` and `CanvasInputPolicy.Modal` through the actual screen setup. Bind Back/confirm and restore focus through the controller. The prefab alone does not pause gameplay, close itself, or perform an action. Stretch its root to the screen; do not add another global input router.

## Typography, artwork, and animation

Palette: near-black `#0A0A0A`, charcoal `#1D1D1D`, warm white `#F6F6F1`, gold `#FFAB00`, muted gray `#A7A7A7`. Colors live on normal Unity graphics and text, so prefab variants can change them without a new theme runtime. Keep white body text on dark surfaces and dark text on gold badges.

The kit includes a static fallback TMP atlas generated from Liberation Sans, its source font, and the SIL Open Font License in `Fonts/`. Printable ASCII plus en dash, em dash, and ellipsis are included. The authored header instead uses the project's Mustica Pro SemiBold TMP asset; include that asset when moving the edited header to another project. Choose suitable TMP fallback fonts for other languages or symbols. Shared font/material references avoid per-label material instances.

Layered surfaces use two Images: a slightly larger rotated gold backing, then a dark face. Text and controls draw above both. The header keeps the authored Corner Warp material; `Materials/Persona_ControlWarp.mat` and `Persona_SurfaceWarp.mat` use shallower corners for controls and large content areas. Their shapes are shared rather than allocated per instance. Small sprites in `Art/` remain available for progress fills and markers. Decoration ignores raycasts; existing input surfaces retain their bindings. No custom runtime rendering script or per-frame style updater is required.

The base visual prefabs do not impose a screen transition. Host-project `Assets/Prototypes/Default/Experiments/Presentation/UIAnims` provides reusable `UIAnim` and `UIAnimGroup` components for opening, closing, wipes, and stagger. Read its README and the standalone `Assets/Prototypes/Default/Experiments/Presentation/PersonaUIKit/TransitionDemo` example. Use unscaled time for paused screens and one writer per animated property. A wrapper can animate an entire nested piece while preserving its layout/input root. Include the UIAnims source separately when moving this package into another project; it lives under the host's prototype scripts.

## Extend the kit

For human authoring, open **Tools > UnscriptedEngine > UI > UI Builder > Open Builder**. The host-project guide is `Assets/Prototypes/Default/Experiments/Presentation/PersonaUIKit/UIBuilder.md`. This window creates pieces and larger layouts under a chosen Canvas/container, preserves prefab connections, supports Undo, and can save the result as a new prefab. Editable palette assets allow additional styles and templates without changing the creation backend.

Use an instance override for one screen's content and a variant for a recurring visual change. Keep reusable composites as nested prefab instances so improvements propagate. For a missing responsibility, create a small appropriately named piece, document its slots and states here, and reuse smaller assets where appropriate. Keep application behavior in a screen, widget subclass, or presenter using the project's modular code conventions.

New UI styles belong in a separate `Styles/<StyleName>` directory with their own skill and references. This kit does not override another explicitly selected template.

The local authoring utility lives under `Assets/Prototypes/Default/Experiments/Presentation/PersonaUIKit/Editor`. **Tools > UnscriptedEngine > UI > Persona Kit > Create Missing Prefabs** creates missing assets and refreshes the preview; it preserves existing prefab files. **Open Gallery** opens the overview in Prefab Mode. **Refresh Preview** updates the image after changes. **Validate Prefabs** checks imported scripts/fonts and exercises basic control wiring in an isolated preview scene. Copy/import that prototype separately if this development utility is needed in another project; the finished package prefabs themselves do not require it.

**Apply Layered Backings** updates existing kit pieces in place and refreshes the gallery/menu preview images. It preserves the authored header file and nested prefab links. `PersonaLayeredStyle.Apply` also supplies the same recipe when generating new pieces. Dividers and progress fills keep their simpler geometry.
