---
coverage: Unresolved repository or product issues that require a local workaround
---

# Known Issues

## No Automated Tests

**Affected area:** Repository-wide

**Description:** No tracked C# or TypeScript test suite provides behavioral coverage.

**Workaround:** Run targeted builds, lint, packaging, and manual Extension Development Host validation.

## VS Code Test Scaffolding Is Stale

**Affected area:** `.vscode/launch.json`, `.vscode/tasks.json`

**Description:** The `Extension Tests` launch configuration points to `out/test/suite/index`, and tasks reference `npm: watch-tests`, but `package.json` has no `watch-tests` script and no test source exists.

**Workaround:** Do not use the `Extension Tests` configuration until a test suite and matching scripts are restored.
