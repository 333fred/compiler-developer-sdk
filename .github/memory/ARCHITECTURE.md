---
coverage: Product components, integration boundaries, caching, and primary request flow
---

# Architecture

The .NET Compiler Developer SDK is a VS Code extension for exploring Roslyn syntax trees, IOperation trees, IL, and decompiled C#. Its TypeScript frontend sends custom requests to a C# backend loaded into the C# extension's language server.

## Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| VS Code frontend | `src/extension/` | Views, commands, editor events, and virtual IL/C# documents |
| Syntax backend | `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/SyntaxVisualizer/` | Syntax tree navigation and semantic details |
| IOperation backend | `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/IOperationVisualizer/` | Declaration hierarchy and lazy operation trees |
| IL backend | `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/IlVisualizer/` | Compilation emit, decompilation, and IL disassembly |
| Shared backend support | `Protocol/`, `Util/` | Request models, conversions, and document caches |

## Integration Boundary

- The extension depends on `ms-dotnettools.csharp`; it does not start its own language server.
- `csharpExtensionExports.ts` describes only the external C# extension exports consumed by this app.
- `csharpExtensionLoadPaths` loads the packaged backend DLL into the C# language server.
- The backend compiles against the C# extension's ExternalAccess API version and uses the ExternalAccess assembly supplied by the C# extension at runtime.
- Backend handlers are stateless Compiler Developer SDK services bound to names in `Endpoints.cs`.

## Caching and Lazy Loading

`VisualizerCache<T>` keys data by immutable Roslyn `Document`, so maps are reused for a document version and collectible afterward.

- Syntax node/token/trivia IDs are built once per document version.
- Declaration/symbol IDs are built once per document version.
- IOperation IDs are computed only for symbols that are expanded or selected.
- The frontend requests roots, direct children, and details separately.

## Request Flow

1. A tree expansion, selection change, or command sends a custom request through the C# extension.
2. The backend resolves the Roslyn document and cached IDs.
3. The backend performs the requested targeted lookup or decompilation.
4. Lightweight DTOs return to the frontend for display.
