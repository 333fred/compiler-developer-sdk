---
coverage: Repository-specific constraints that materially affect correctness or responsiveness
---

# Conventions

Formatting and standard language conventions are enforced by `.editorconfig`, TypeScript, and ESLint. This file contains only non-obvious repository constraints.

## Protocol

- Keep endpoint names and JSON property names synchronized between TypeScript and C#.
- Serializer-facing C# properties use both `DataMember` and `JsonPropertyName` for C# extension compatibility.
- Keep responses lightweight; use document-version-local numeric IDs instead of serializing Roslyn object graphs.

## Performance

- Cache document-derived maps with `VisualizerCache<T>` and keep IOperation computation lazy per containing symbol.
- Tree providers fetch direct children and defer semantic/property details until expansion.
- Use targeted Roslyn lookups in request hot paths; reuse semantic models and propagate cancellation.
- Selection handlers check for C# documents and visible views. Preserve `editorChangeCausedDataChange`.
- Do not block asynchronous work with `.Result` or `.Wait()`.
- Preserve the IL service's parallel emit/context lookup and minimal decompiler settings.

## Service and Integration Boundaries

- Request handlers are stateless; document-derived state belongs in dedicated cache services.
- Pin required transitive NuGet updates centrally in `Directory.Packages.props`.
- Runtime backend dependencies must be copied into `dist` by `VscePrepublish`, except assemblies supplied by the host C# extension, such as the Compiler Developer SDK ExternalAccess assembly.
- Do not edit generated `dist/`, `out/`, `bin/`, or `obj/` artifacts.

## CI and Workflow Security

- GitHub Actions workflows must declare explicit least-privilege `permissions` instead of relying on repository defaults.

## Errors and Logging

- Surface failures for interactive commands; do not add broad catches or silent success fallbacks.
- Keep event-loop and traversal logging behind verbose logging and out of tight loops.
