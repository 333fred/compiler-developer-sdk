---
coverage: Repository directories, ownership, status, and notable entry points
---

# File Map

| Path | Status | Purpose and notable entry points |
|------|--------|----------------------------------|
| `src/extension/` | Active | TypeScript frontend. `extension.ts` activates the extension; the visualizer provider files own the trees; `ilVisualizer.ts` owns virtual decompilation documents. |
| `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/SyntaxVisualizer/` | Active | Syntax cache and custom syntax endpoints. |
| `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/IOperationVisualizer/` | Active | Symbol/IOperation cache and custom operation endpoints. |
| `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/IlVisualizer/` | Active | Emit, assembly resolution, decompilation, and disassembly. |
| `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/Protocol/` | Active/vendor-derived | Minimal LSP protocol types and System.Text.Json conversions; see its third-party notice. |
| `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/Util/` | Active | Shared cache and response helpers. |
| `.github/memory/` | Active docs | On-demand repository knowledge; `INDEX.md` is the loading map. |
| `.github/skills/update-docs/` | Active docs | Mandatory knowledge-base maintenance checklist. |
| `.github/workflows/` | CI config | Build and VSIX packaging workflow. |
| `.vscode/` | Config/stale test entries | Extension Host launch, .NET attach, and watch tasks. Test entries are currently stale. |
| `images/` | Assets | Marketplace icons and README demonstrations. |
| `dist/` | Generated | Webpack output and runtime DLLs packaged into the VSIX. |
| `out/` | Generated/stale | Old compiled output; no active tracked test suite produces it. |

## Root Entry Points

| File | Purpose |
|------|---------|
| `package.json` | Extension contributions, settings, dependencies, and npm scripts |
| `src/Microsoft.CodeAnalysis.CompilerDeveloperSdk/Microsoft.CodeAnalysis.CompilerDeveloperSdk.csproj` | Backend build and `VscePrepublish` runtime copy target |
| `Directory.Packages.props` | Central NuGet versions |
| `nuget.config` | NuGet sources and source mapping |
| `webpack.config.js` | Frontend bundle |
| `tsconfig.json`, `eslint.config.mjs`, `.editorconfig` | Enforced source conventions |
| `README.md` | User-facing documentation and release notes |
