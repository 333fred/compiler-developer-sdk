---
coverage: VS Code extension contributions and custom TypeScript/C# request contracts
---

# API Map

The repository's public integration surface consists of VS Code contributions and custom request contracts implemented by the TypeScript frontend and C# language-server backend.

## VS Code Views

| View ID | Container | Purpose | Visibility |
|---------|-----------|---------|------------|
| `syntaxTree` | `csharp-syntax-visualizer` | Lazy Roslyn syntax node/token/trivia tree | Syntax visualizer enabled and backend loaded |
| `operationTree` | `csharp-syntax-visualizer` | Lazy declaration, symbol, and IOperation tree | IOperation visualizer enabled and backend loaded |

## VS Code Commands

| Command | Purpose |
|---------|---------|
| `compilerDeveloperSdk.highlightOnClickSyntax` | Toggle syntax-node source highlighting. |
| `compilerDeveloperSdk.highlightOnClickIOperation` | Toggle IOperation-node source highlighting. |
| `compilerDeveloperSdk.collapseSyntaxVisualizer` | Collapse all syntax tree nodes. |
| `compilerDeveloperSdk.collapseIOperationVisualizer` | Collapse all operation tree nodes. |
| `compilerDeveloperSdk.decompileContainingContext` | Emit and show decompiled C# and IL for the active source context. |

## VS Code Settings

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `compilerDeveloperSdk.enableSyntaxVisualizer` | boolean | `true` | Show the syntax visualizer. |
| `compilerDeveloperSdk.enableIOperationVisualizer` | boolean | `true` | Show the IOperation visualizer. |
| `compilerDeveloperSdk.syncCursorWithTree` | boolean | `true` | Reveal source ranges when tree nodes are selected. |
| `compilerDeveloperSdk.verboseLogging` | boolean | `false` | Enable debug-level extension logging. |

## Custom Request Endpoints

| Endpoint | Request | Response | Purpose |
|----------|---------|----------|---------|
| `syntaxTree` | document + optional `parentNodeId` | syntax node array | Get the root or direct syntax children. |
| `syntaxTree/nodeAtRange` | document + range | optional syntax node | Find the nearest node/token for an editor selection. |
| `syntaxTree/parentNode` | document + `childId` | optional parent node | Support VS Code tree reveal/navigation. |
| `syntaxTree/info` | document + syntax node DTO | semantic/property DTO | Get deferred syntax details. |
| `operationTree` | document + optional `parentSymbolId` | operation tree node array | Get the symbol root or declaration children. |
| `operationTree/operationChildren` | document + symbol ID + optional operation ID/property | operation tree node array | Get IOperation roots or a named property group. |
| `operationTree/nodeAtRange` | document + range | optional operation tree node | Find the most specific symbol/operation for a selection. |
| `operationTree/parentNode` | document + symbol ID + optional operation ID | optional parent node and relationship | Traverse operation and symbol parents. |
| `il/containingSymbol` | document + position | success/error union | Emit and return decompiled C# and IL. |

## Compatibility Rules

- Endpoint strings must remain identical in `Endpoints.cs` and frontend `RequestType` declarations.
- JSON property names must remain identical across TypeScript interfaces and C# request/response models.
- Protocol models and converters use System.Text.Json attributes and APIs.
- Contributions in `package.json` are part of the user-visible extension API and require matching implementation registration.
