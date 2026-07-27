---
name: update-docs
description: >
  Update the compiler-developer-sdk knowledge base after code or configuration changes. Run at
  the end of every task that modifies code, adds files, changes extension contributions or LSP
  contracts, or establishes new performance and implementation patterns.
---

# Update Docs

Run at the end of every task that changes code or repository configuration. This is mandatory.

Keep memory concise. Record durable constraints, boundaries, and gotchas that affect future decisions; do not narrate implementation details that are easy to read from code or duplicate information already covered elsewhere.

## Checklist

**Files or directories added, moved, removed, or repurposed?** Update `.github/memory/FILE_MAP.md`.

**Memory file added, removed, renamed, or had its purpose change?** Update `.github/memory/INDEX.md` and every memory file that references it.

**VS Code command, view, setting, request endpoint, or protocol DTO implemented by this repository changed?** Update `.github/memory/API_MAP.md`.

**Component boundaries, request flow, cache ownership, or packaging flow changed?** Update `.github/memory/ARCHITECTURE.md`.

**New repository-specific implementation, performance, protocol, error-handling, or async constraint established?** Update `.github/memory/CONVENTIONS.md` or create a focused memory file.

**Unresolved repository or product issue requires a local workaround?** Update `.github/memory/KNOWN_ISSUES.md`. Do not add ordinary implementation details, platform behavior, missing coverage, or issues that should simply be fixed.

**User-facing feature, setting, requirement, limitation, or release behavior changed?** Update `README.md`.

**Any documentation updated?** No separate tracking file is required; git history records maintenance.

## Creating New Memory Files

If knowledge does not fit an existing file:

- Create a focused file in `.github/memory/` with a descriptive name.
- Add minimal YAML frontmatter with one `coverage:` field.
- Add the file to `.github/memory/INDEX.md` with purpose and loading guidance.
- Keep claims verified against current source and configuration.

## Frontmatter Format

```yaml
---
coverage: Brief description of what this document covers
---
```

Do not add dates, authors, confidence scores, or last-updated fields. Git provides that history.
