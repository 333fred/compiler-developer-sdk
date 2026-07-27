---
coverage: Index and loading map for all .github/memory/ knowledge-base files
---

# Memory Index

This is the loading map for the repository knowledge base under `.github/memory/`. Read this file first when starting a task, then load only the files relevant to the work.

## Loading Map

| File | Purpose | When to load |
|------|---------|--------------|
| **`INDEX.md`** (this file) | Discovery map for repository knowledge | Always; read first |
| **`ARCHITECTURE.md`** | Product components, key abstractions, and request data flow | For all non-trivial tasks |
| **`CONVENTIONS.md`** | Repository-specific performance, protocol, caching, and integration constraints | When writing or reviewing code |
| **`FILE_MAP.md`** | Directory ownership, status, dependencies, and notable files | When locating code or adding/moving files |
| **`API_MAP.md`** | VS Code contributions and custom C#/TypeScript LSP contracts | When changing commands, settings, views, or protocol types |
| **`KNOWN_ISSUES.md`** | Unresolved repository or product issues requiring a local workaround | When working in an affected area |

## Conventions

- Treat memory files as authoritative for repository conventions, but verify claims against current code before relying on them.
- Prefer small, focused files over monolithic notes.
- New memory files must use minimal YAML frontmatter containing only a `coverage:` field.

## Maintenance

- Added, removed, or renamed a memory file: update this index.
- Changed a memory file's purpose: update its row in the loading map.
- Do not list files that do not exist.
