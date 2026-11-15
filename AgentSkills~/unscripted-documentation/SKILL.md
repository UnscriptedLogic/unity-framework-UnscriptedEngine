---
name: unscripted-documentation
description: Write or update short, simple technical documentation for systems, tools, APIs, and workflows in this project. Use for READMEs, guides, and system explanations, with practical code snippets, diagrams, and clear analogies.
---

# Unscripted documentation

Write technical documentation that readers can understand quickly and act on. Use plain language, short sentences, and precise names. Keep normal grammar in documentation; concise does not mean cryptic. Follow explicit user formats and established document templates when supplied.

## Ground explanations in the system

- Read the relevant implementation and existing documentation before describing behavior. Use real API names, paths, defaults, and setup steps. Identify uncertainty instead of inventing functionality.
- Explain the public workflow first. Include internals only when they help readers use, debug, or extend the system.
- Keep each fact in one place. Link to existing details instead of repeating them. For small documentation fixes, preserve the surrounding structure rather than expanding the whole document.

## Default sections

Use these sections for a system or feature guide. Keep each to a short paragraph, small list, or focused example; add detail only when needed for correct use.

### 🎯 What it does

State the purpose, problem solved, and expected result. Identify the main pieces without listing every class.

### 🛠️ How to use it

Give prerequisites and the shortest working setup, followed by the normal usage flow and expected output. Include a useful code snippet or concrete configuration example. Use numbered steps when order matters.

### ⚙️ How it works

Explain the main flow, responsibilities, and ownership. Add a small diagram when it makes relationships or execution order easier to see. For difficult concepts, use a short familiar analogy, then map it to the actual technical pieces and explain where the analogy stops being accurate.

### 🧩 How to extend it

Point to the actual extension seam: a function, interface, provider, configuration, or composition point. Show a small example when useful. Explain required contracts and cleanup without inventing extension hooks or adding architecture merely for the documentation.

### ⚠️ Limitations

State known constraints, unsupported cases, and relevant tradeoffs. Include browser restrictions, runtime costs, lifecycle requirements, or domain-reload behavior when they apply. Distinguish verified limits from untested behavior. If no supported extension point or no confirmed limitation was found, say so briefly.

For narrow reference entries or short edits, use only the sections that help; do not create empty sections or force a full guide into every comment.

## Examples and visual explanations

- Use short, focused fenced code blocks with a language tag. Show enough context to identify where code belongs and what it does. Mark pseudocode and omitted setup clearly; do not present illustrative fragments as tested, complete programs.
- Match the project's coding conventions, including explicit variable types in C# and correct subscription/resource cleanup. Prefer existing public APIs over reaching into private implementation.
- Use Mermaid for small flow or relationship diagrams when the destination supports it; otherwise use a readable text diagram. Label nodes with real system terms and keep the diagram consistent with the explanation.
- Use a few relevant emojis, such as the section markers above. Keep identifiers, code, commands, and diagrams free of decorative emojis.
- Use analogies for difficult concepts, not every paragraph. For example, a provider can be described as a replaceable LEGO piece: the consumer keeps the workflow, while the provider supplies one variable decision through a shared contract.

## Final check

Confirm a reader can identify the purpose, perform the normal use case, find the extension point, and recognize the limits. Check snippets and diagrams against the implementation. Report whether examples were executed or only inspected when that distinction matters. Remove repeated explanations and decorative material that adds length without understanding.
