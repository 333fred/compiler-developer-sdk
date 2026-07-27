# compiler-developer-sdk - Copilot Instructions

## Project Overview

This VS Code extension visualizes Roslyn syntax trees, IOperation trees, IL, and decompiled C# in real time.

- `src/extension/` contains the TypeScript VS Code frontend.
- `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/` contains custom C# services loaded into the C# extension language server.

Performance matters because visualizers update while users type.

## Project Structure

```text
src/
  extension/                                      # VS Code frontend
  Microsoft.CodeAnalysis.CompilerDeveloperSdk/
    SyntaxVisualizer/                            # Syntax services/cache
    IOperationVisualizer/                        # Operation services/cache
    IlVisualizer/                                # Emit/decompilation
    Protocol/                                    # Request models
    Util/                                        # Shared helpers
.github/
  memory/                                        # Repository knowledge
  skills/update-docs/                            # Documentation maintenance
```

## Build and Test

```bash
# Backend changes
dotnet build src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/Microsoft.CodeAnalysis.CompilerDeveloperSdk.csproj

# Frontend changes
npm run compile
npm run lint

# Packaging changes
npm run vscode:prepublish
```

There is no tracked automated test suite. Use targeted builds and manually validate affected views or commands in the Extension Development Host.

## Code Style

- Follow `.editorconfig`, strict TypeScript, and ESLint.
- Keep TypeScript/C# protocol contracts synchronized.
- Preserve lazy loading, document-version caching, cancellation, and event guards.
- Do not edit generated output.
- Load `.github/memory/CONVENTIONS.md` when changing implementation.

## Validation Checklist

1. Read `.github/memory/INDEX.md`.
2. For non-trivial work, read `ARCHITECTURE.md` and `CONVENTIONS.md`.
3. Build the modified frontend or backend.
4. Run `npm run lint` for TypeScript changes.
5. Validate packaging when changing manifests, dependencies, load paths, or runtime assets.
6. Manually validate affected interactive behavior when practical.
7. Run the `update-docs` skill.

## Agent Orientation

1. Read `.github/memory/INDEX.md` first.
2. Load `ARCHITECTURE.md` and `CONVENTIONS.md` for non-trivial work.
3. Load other memory files only as directed by the index and task.
4. Verify memory claims against current code.
5. Run the update-docs skill after making changes.

### Memory

`.github/memory/` is the persistent knowledge base. Keep it small and focused. Correct stale claims immediately and record only durable knowledge that will help future work.

New memory files use only `coverage:` frontmatter and must be added to `INDEX.md`.

### Documentation Update Obligation

- File ownership changed: update `FILE_MAP.md`.
- Commands, settings, endpoints, or DTOs changed: update `API_MAP.md`.
- Component boundaries, caching, or request flow changed: update `ARCHITECTURE.md`.
- A non-obvious constraint was established: update `CONVENTIONS.md`.
- A limitation or workaround was found: update `KNOWN_ISSUES.md`.
- User-facing behavior changed: update `README.md`.

### Skills

Repository skills live under `.github/skills/<skill-name>/SKILL.md`. Run `update-docs` at the end of code-changing tasks.

### Rules

- Keep request handlers stateless and document-derived state in cache services.
- Avoid repeated whole-tree or semantic work in interactive paths.
- Do not add broad catches, silent failures, or success-shaped fallbacks.
- Ensure required runtime backend dependencies are copied into the VSIX.
