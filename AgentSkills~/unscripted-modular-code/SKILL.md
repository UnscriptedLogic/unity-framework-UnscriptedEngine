---
name: unscripted-modular-code
description: Write or refactor code in UnscriptedLogic's reusable LEGO-piece style with descriptive functions, clear ownership, small composable modules, and practical provider workflows. Use for the user's personal coding style or requests for this modular approach across applications, services, tools, web, and games in any language.
---

# UnscriptedLogic modular code

Build from understandable, reusable pieces with few independent mechanisms. A change to one policy or integration should usually stay inside that piece or its assembly point. Reduce duplicated logic and unnecessary wiring together. Fewer files alone does not mean simpler design.

This skill is platform independent. Follow the target repository's conventions and actual APIs. Unity inspired the examples; its classes, lifecycle, networking, naming prefixes, and asset model are not prerequisites. This skill defines code structure and workflows, not a UI aesthetic.

## Work from the existing pieces

Before adding a mechanism, find the current owner, nearest working example, and existing operation that could do the job. Trace enough of the call path to understand initialization, state changes, and cleanup. Reuse the authoritative implementation when its behavior fits. Extend a small seam or adapt inputs before duplicating a workflow.

Keep changes within the requested feature or refactor. Report nearby bottlenecks, redundant mechanisms, unclear ownership, and tightly coupled code with concrete locations and consequences; distinguish demonstrated problems from suspected costs. Do not turn discoveries into an unrelated rewrite.

Choose the smallest composition that covers the real requirements. Existing language features, library operations, and a few named functions often suffice. Introduce a new module, provider, interface, or service when it creates a useful independent responsibility, replacement boundary, or shared implementation. Do not create layers merely to satisfy a pattern.

## Responsibilities and ownership

- Give each operation or state transition one authoritative owner. Other pieces request changes through that owner's API. Expose only the state callers need, using read-only access where possible.
- Keep reusable behavior independent of the particular screen, feature, transport, scene, or application that assembles it. Place application-specific choices in configuration, a provider, or a thin caller.
- Prefer composition over long inheritance chains. A stable base hook can be appropriate in a framework that already uses it; do not build a hierarchy for every variation.
- Keep shared definitions and recipes immutable where practical. Put mutable execution state in the instance, request, session, or job that owns it. Share a stateful provider only when that ownership and concurrency behavior are intentional.
- Keep dependencies visible through parameters, constructors, factory arguments, or the platform's established composition mechanism. Reuse the existing mechanism; avoid a second container, hidden global lookup, or generic service locator.
- Keep assembly close to the application boundary so a caller can discover how pieces connect. Avoid chains of forwarding objects that hide a simple call.

## Provider workflows

A provider supplies a variable decision, value, definition, or data source through a focused contract. The consumer owns the surrounding workflow. Use a plain callback or concrete object when sufficient; use an interface or abstract provider when actual substitution or an existing framework makes it useful.

Choose the seam by asking what should change independently. For example, the caller chooses when to export; a provider supplies the records; the existing export operation validates and writes them. A provider choosing records should not also own UI navigation and unrelated persistence. If a provider is explicitly the storage boundary, its contract may perform storage I/O; describe that responsibility clearly.

At a provider boundary, make these details clear in names, types, or a short contract:

- Required context and the result's meaning, including units or ordering when relevant.
- Whether no result is valid, how failures are reported, and whether the operation is synchronous or asynchronous.
- Which side owns results, mutable buffers, resources, subscriptions, and any state that outlives the call.
- Whether calls may repeat or overlap, whether side effects occur, and how cancellation or retry works when applicable.

Pass only context the decision needs. A context object can help when several values belong together; do not pass an entire application container to avoid designing the input. Prefer meaningful result types over string-keyed object bags. For caller-owned output buffers, document append/clear behavior and avoid retaining references unexpectedly.

Keep results, validation, and mutation connected through a readable workflow. Where partial changes would be invalid, prepare and validate before committing through the existing mutation API. For random or external results, preserve an accepted result across retries when recomputing would change its meaning. Reuse existing transaction/idempotency support where required; do not introduce distributed machinery for a local operation.

Do not convert a synchronous feature to async or poll every frame just because another provider might someday need it. Pick the contract the current requirements need. Extend it deliberately when a real new variant arrives.

## Functions that explain the work

Use descriptive action names that reveal intent, such as `CollectEligibleRecords`, `ValidateDestination`, `ApplySettings`, or `ReleaseSubscription`. Prefer domain terms to vague names such as `HandleData`, `ProcessStuff`, or `Manager2`. A coordinating function should read as a short sequence of meaningful operations.

Keep related lines together. Extract a function when it names a coherent decision or action, removes meaningful duplication, isolates a dependency, or makes a workflow easier to follow. Do not split every conditional or one-line operation into helpers that force constant jumping between files. Avoid giant methods that mix input, policy, storage, and presentation.

Use early exits when they clarify prerequisites. Make side effects and expected failure paths evident. Avoid surprising work in getters. Prefer private fields and a narrow public API. In statically typed code, use explicit variable types where supported, following this user's preference; adapt syntax to the target language rather than importing C# conventions wholesale.

Keep function names and comments aligned with actual behavior. Comments explain ownership, constraints, or reasons that code cannot express; they should not restate each line. Add brief setup/use/replace/cleanup guidance when a new reusable piece would otherwise be difficult to assemble.

## Reuse without extra failure points

Reuse stable domain operations before building generic infrastructure. Similar-looking code with different rules may deserve separate functions; genuine shared behavior deserves one implementation. Prefer a small parameter or configuration value when variation is data. Prefer a provider when the decision or source varies. Prefer an adapter when an external API must fit an existing contract.

Keep shared contracts small and expressed in the consumer's terms. Consumers should not need to recognize every concrete provider with type checks or switches. If adding a variant requires edits across unrelated consumers, look for policy leaking across the boundary. A simple finite switch remains appropriate when it expresses one cohesive, closed decision.

Do not anticipate every future application. Avoid plugin registries, inheritance ladders, event buses, custom frameworks, reflection discovery, and flag-heavy universal components unless current needs or existing architecture justify them. Reuse an established event channel for genuine independent observers; prefer a direct call for a known request with one owner.

## Lifecycle, performance, and failure

The owner of a resource or subscription must release it at the matching lifecycle boundary. Consider repeated initialization, cancellation, teardown, reconnect, hot reload, or re-entry as applicable. Clean up even when an operation fails partway through. Keep concurrency choices and shared mutable state explicit.

Reuse the project's existing scheduler, storage, input, logging, rendering, and event mechanisms where they fit. Avoid parallel systems competing to mutate the same state. Validate untrusted input and configuration where they enter the system; handle expected absence without hiding programming errors behind silent success or catch-all fallbacks.

Keep work proportional to actual demand. Watch for repeated full scans, per-update allocation, duplicate subscriptions, redundant serialization, and requests that fan out unnecessarily. Bound retries and expensive searches. Cache only with a clear owner and invalidation rule. Optimize demonstrated hot paths or obvious avoidable work; do not add a cache or pool to every function.

## Review the resulting composition

Check that the main workflow is easy to find, each piece has a clear owner, and a new real provider can fit without editing the shared execution logic. Confirm that callers use existing mutation APIs instead of reimplementing invariants. Review missing input, provider failure, repeated calls, cancellation, and cleanup as relevant to the feature.

Use the target project's normal build and focused behavioral checks for changed code. Prefer checks of observable outcomes and boundaries over tests that mirror internal helper structure. Report what was checked and any remaining integration limits. Do not claim that this architecture alone proves correctness or performance.

Read [patterns and examples](references/patterns.md) when choosing between a function, configuration, provider, adapter, or coordinator. This folder is self-contained and can be copied to another project's or agent host's skill directory without the original Unity package.
