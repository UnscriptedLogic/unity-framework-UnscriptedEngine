# UI font replacement

Open **Tools / UnscriptedEngine / UI / Font Replacement**. Select a UI root in a scene or open a prefab in Prefab Mode and select its root. Multiple selected roots are supported; overlapping hierarchies are deduplicated. The tool handles both `TextMeshProUGUI` and world-space `TextMeshPro` components in the current editor stage.

1. Keep **Include inactive** enabled to include hidden pages, popups and disabled UI.
2. Expand a detected source-font group and assign **Replace with**. Each source matches by exact font asset, so title and body fonts can be replaced independently. A → B and B → C operate on original fonts; they do not cascade.
3. Enable only the formatting properties to change for that group. Unchecked properties retain each text's individual values. Leave replacement empty for formatting-only edits.
4. Review affected components and before/after property values, then click **Apply**. The review is a change list, not a rendered typography preview. Inspect the actual scene/prefab after applying; one Undo restores the entire operation.
5. Save the scene or prefab normally. Applying marks changes dirty and records prefab instance overrides; it does not save assets automatically or apply instance overrides back to their source prefab.

**Lock selection** keeps the same roots while inspecting other objects. **Rescan** discovers hierarchy/font changes. New numeric override values start from the first text found for each font; enabling one explicitly assigns that value to the entire matching group. No properties are enabled by default.

Available overrides: character spacing, word spacing, line spacing, paragraph spacing, font size, auto-size minimum/maximum, auto sizing, font style, alignment, wrapping, color, and margins (left/top/right/bottom). Spacing uses TextMeshPro Inspector units. Rich-text tags, auto sizing, gradients/materials and runtime scripts can affect the final appearance independently of these component properties.

Swapping a font resets that component to the replacement font's default shared material. Custom material presets belonging to the old font atlas cannot safely be reused; assign compatible replacement presets afterward if needed. Formatting-only changes preserve the existing material. Font assets and fallback-font lists are never edited.

Project prefab assets must be opened in Prefab Mode before editing. Text outside the selected hierarchies/current stage is excluded. Play Mode application is disabled. Invalid numeric values and auto-size ranges are checked before modifying any component. Legacy Unity Text and UI Toolkit text are outside this tool's scope.

The window scans hierarchy only when selection/hierarchy changes or a rescan is requested. Review shows up to 100 affected components; Apply still includes all matches. Editor-only code adds no player runtime work. Tests cover mixed-font mapping, non-cascading replacements, inactive/overlapping selections, selective formatting, validation, Undo and prefab instance overrides.
