# Choosing and assembling reusable pieces

## Select the smallest useful boundary

| Need | Usually start with | Reason to add more |
| --- | --- | --- |
| Name a cohesive calculation or decision | A descriptive function | Shared state or lifecycle becomes part of the responsibility |
| Change thresholds, colors, limits, or labels | Typed configuration | Behavior or data source itself changes |
| Swap a selection rule, recipe, or data source | A provider function or object | Multiple implementations or framework integration warrants an explicit contract |
| Connect an external API to existing behavior | A thin adapter | Keep API-specific types and errors from spreading into consumers |
| Execute several existing operations in a sequence | A coordinating function | Persistent execution state or lifecycle warrants an instance |
| Notify independent observers after a change | Existing notification mechanism | Add infrastructure only if current delivery requirements exceed it |

These are choices, not mandatory layers. One function can be the entire implementation when it expresses the responsibility well.

## A provider in an ordinary application

Suppose an application already has CSV generation and file storage. A new reporting screen needs either sales records or support records. Keep the screen responsible for the user action; let a record provider select the data; reuse the CSV and storage operations. Do not build a new export engine per record source.

Illustrative TypeScript, assuming the named operations and domain types already exist in the target application:

```typescript
type ReportSource = (query: ReportQuery) => Promise<readonly ReportRecord[]>;

async function ExportReport(
    query: ReportQuery,
    loadRecords: ReportSource,
    output: ReportOutput,
): Promise<ReportReceipt> {
    const records: readonly ReportRecord[] = await loadRecords(query);
    ValidateReportRecords(records);
    const csv: string = BuildReportCsv(records);
    return output.Write(csv);
}
```

The async shape fits an actual remote source in this example; local synchronous work need not adopt it. Use target-language naming conventions. The existing output boundary owns storage details and exposes its real failure behavior. The coordinator should not swallow a write failure and return a success receipt. Add cancellation, streaming, or batch limits when the actual size and lifetime require them, using established facilities.

A second source should fit the same consumer-facing record contract without `if salesProvider` checks in CSV generation. If two exports have fundamentally different schemas or policies, share only the genuinely common operations; do not accumulate mode flags in one universal exporter.

## Patterns grounded in UnscriptedEngine

These observations explain the author's preferences; they are not dependencies of this skill. Paths are relative to the original package and may not exist after copying the skill elsewhere. Do not reproduce every incidental implementation detail as a rule.

- **NPC roaming:** `Framework/AI/NpcTargetProvider.cs` supplies a destination. The roaming controller owns timing; navigation adapts steering into the existing character motor. The provider changes where an NPC goes without replacing collision, gravity, or replication. General lesson: separate the variable choice from stable execution and reuse its existing owner.
- **Loot:** `Framework/Inventory/ChestLootProvider.cs` appends generated contents to a caller-owned list. Provider assets are shared recipes; they do not mutate inventories or store per-chest state. `UChest` caches generated contents and calls the existing inventory batch operation so a capacity retry does not reroll rewards. General lesson: separate proposal from commit, keep runtime state local, and preserve the meaning of retry.
- **Settings:** `Framework/Settings/SettingsProvider.cs` supplies definitions and owns a disposable registration for its active lifetime. The settings service owns effective values; UI and consumers use that same service. General lesson: reuse one authoritative operation and pair acquisition with release.
- **Interaction:** `IInteractionTargetProvider` exposes a focused target for raycast or proximity implementations. Consumers need the result, not knowledge of how targeting works. General lesson: make a small contract in the consumer's terms.

For Unity work specifically, follow that repository's local instructions about serialized fields, tooltips, Inspector visualization, namespaces, and disabled domain reload. For services, tools, or web applications, use the matching host lifecycle: request/session completion, disposal, component unmount, shutdown, or hot reload. Do not require a Unity-style enable/disable cycle where none exists.

## Detect accidental complexity

- A new source requires consumer type checks: the contract probably leaks implementation details.
- A simple request crosses several pass-through services: collapse layers that do not own a decision, invariant, or integration boundary.
- Several features implement the same mutation or validation differently: inspect the existing owner and share the real invariant.
- One component has many unrelated mode flags: separate independent responsibilities or move the varying policy to a provider.
- Every operation requires a global application object: narrow dependencies to the context actually used.
- Adding reuse requires copying initialization and teardown everywhere: give lifecycle ownership a clear home or reuse the platform's established registration mechanism.
- Shared caching makes it unclear who can mutate or invalidate results: restore explicit ownership before optimizing further.
- A small change causes large unrelated edits: inspect coupling before adding another general abstraction.

Use these as investigation cues. Confirm the behavior and cost in the actual code before reporting a defect or proposing a broad refactor.
