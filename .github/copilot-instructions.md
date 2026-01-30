# Copilot Instructions - Performance Guidelines

This VS Code extension visualizes Roslyn compiler internals (syntax trees, IOperation trees, IL). Performance is critical since visualizers update in real-time as users type.

## Architecture Overview

- **C# Backend** (`src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/`): LSP services that interact with Roslyn APIs
- **TypeScript Frontend** (`src/extension/`): VS Code extension that communicates via the C# extension's language server

## Caching Patterns

### C# Side
The backend uses `ConditionalWeakTable`-based caching via `VisualizerCache<T>`. Follow this pattern for document-level data:

```csharp
// Extend VisualizerCache<T> for new cached data structures
// Use GetOrAddCachedEntry() to lazily build and cache per-document data
protected async Task<TEntry> GetOrAddCachedEntry(Document document, Func<Task<TEntry>> factory)
```

- `DocumentSyntaxInformation`: Caches syntax node ID mappings per document
- `DocumentIOperationInformation`: Caches symbol/IOperation ID mappings per document

**Key principle**: Build ID maps once per document version, reuse on subsequent requests.

### TypeScript Side
Tree data providers cache node data in memory. When modifying providers:
- Preserve the `_nodeMap` pattern for O(1) node lookups by ID
- Clear caches appropriately on document changes via `_onDidChangeTreeData` events

## Async & Cancellation

### C# Services
All LSP handlers are async. When adding new services:
- Use `async ValueTask<T>` for lightweight operations
- Pass `CancellationToken` through to Roslyn APIs that support it
- Avoid blocking calls; prefer `await` over `.Result` or `.Wait()`

```csharp
// Good: Roslyn APIs support cancellation
var root = await document.GetSyntaxRootAsync(cancellationToken);
var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
```

### TypeScript Side
- Event handlers should not block the extension host
- Use `async/await` consistently; avoid `.then()` chains
- The `editorChangeCausedDataChange` flag pattern prevents redundant tree updates—preserve this when modifying event handlers

## Tree Traversal Performance

### Syntax Trees
When traversing syntax, prefer targeted methods over full tree walks:

```csharp
// Prefer: Targeted lookup
var node = root.FindNode(textSpan);
var token = root.FindToken(position);

// Avoid in hot paths: Full tree enumeration
root.DescendantNodesAndTokensAndSelf(descendIntoTrivia: true)
```

Only include trivia in traversal when explicitly needed (e.g., building the initial ID map).

### IOperation Trees
Semantic operations are more expensive than syntax operations:

```csharp
// Cache and reuse SemanticModel within a request
var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

// GetOperation() is relatively cheap once you have the model
var operation = semanticModel.GetOperation(syntaxNode, cancellationToken);
```

## Event Handling

### Document Changes
Both visualizer providers refresh on `onDidChangeTextDocument`. When modifying this:
- Only refresh for C# files (`document.languageId === 'csharp'`)
- The refresh invalidates cached data, triggering lazy rebuilds on next request

### Cursor Position Changes
`onDidChangeTextEditorSelection` triggers node-at-range queries. Current implementation:
- Checks `treeView.visible` before making requests
- Uses `editorChangeCausedDataChange` to avoid redundant updates

Maintain these guards when adding new selection-based features.

## Request/Response Patterns

### Protocol Design
Requests use URI + Position/Range. Responses return node arrays with IDs for lazy child loading:

```typescript
// Lazy loading pattern: getChildren() fetches only when expanded
async getChildren(element?: TreeNode): Promise<TreeNode[]> {
    // Root: fetch top-level nodes
    // Element: fetch children by parent ID
}
```

### Keep Responses Lightweight
- Return only data needed for tree display (ID, label, kind, span)
- Defer expensive data (full properties) to separate info requests
- Use numeric IDs instead of serializing full node structures

## IL Decompilation

The `IlForContainingSymbolService` is the most expensive operation. It:
1. Emits compilation to memory stream (parallel task)
2. Decompiles using ICSharpCode.Decompiler

When modifying IL visualization:
- Preserve the parallel emit pattern
- Use minimal `DecompilerSettings` (avoid unnecessary features)
- Handle emit failures gracefully with user-friendly error messages

## Consistency Between Visualizers

Keep Syntax Tree and IOperation Tree visualizers consistent in:
- Caching strategies
- Event handling patterns
- Tree item presentation (icons, collapsible states)
- Error handling and logging

When adding features to one visualizer, consider if it applies to the other.

## Logging

Use the `Logger` abstraction with verbose logging guarded by configuration:

```typescript
if (verboseLogging) {
    logger.log(`Detailed debug info: ${data}`);
}
```

Avoid logging in tight loops or per-keystroke handlers unless verbose mode is enabled.
