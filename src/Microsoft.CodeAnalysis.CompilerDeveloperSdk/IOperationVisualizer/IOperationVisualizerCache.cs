using System.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

sealed class IOperationVisualizerCache : AbstractCompilerDeveloperSdkLspService
{
    private readonly ConditionalWeakTable<Document, DocumentIOperationInformation> _cache = new();

    public bool TryGetCachedEntry(Document document, [NotNullWhen(true)] out DocumentIOperationInformation? entry)
    {
        return _cache.TryGetValue(document, out entry);
    }

    private void SetCachedEntry(Document document, DocumentIOperationInformation entry)
    {
        _cache.Add(document, entry);
    }

    public async Task<DocumentIOperationInformation> GetOrAddCachedEntry(Document document, CancellationToken cancellationToken)
    {
        if (!TryGetCachedEntry(document, out var entry))
        {
            entry = await DocumentIOperationInformation.CreateFromDocument(document, cancellationToken).ConfigureAwait(false);
            SetCachedEntry(document, entry);
        }

        return entry;
    }
}

// TODO: This won't work if there are mutliple partial parts in one file. Need to map from node to syntax+symbol to handle this
sealed record DocumentIOperationInformation(IReadOnlyDictionary<int, ISymbol> IdToSymbol, IReadOnlyDictionary<ISymbol, int> SymbolToId)
{
    public static async Task<DocumentIOperationInformation> CreateFromDocument(Document document, CancellationToken ct)
    {
        var (idToSymbol, symbolToId) = await BuildIdMap(document, ct);
        return new DocumentIOperationInformation(idToSymbol, symbolToId);

        static async Task<(Dictionary<int, ISymbol> idToSymbol, Dictionary<ISymbol, int> symbolToId)> BuildIdMap(Document document, CancellationToken ct)
        {
            // First time we've seen this file. Build the map
            int id = 0;
            var idToSymbol = new Dictionary<int, ISymbol>();
            var symbolToId = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);

            var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);

            Debug.Assert(root != null);
            Debug.Assert(model != null);

            foreach (var decl in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(decl, ct) is { } symbol)
                {
                    idToSymbol[id] = symbol;
                    symbolToId[symbol] = id;
                    id++;
                }
                else if (root is FieldDeclarationSyntax { Declaration.Variables: { } variables })
                {
                    foreach (var variable in variables)
                    {
                        if (model.GetDeclaredSymbol(variable, ct) is { } variableSymbol)
                        {
                            idToSymbol[id] = variableSymbol;
                            symbolToId[variableSymbol] = id;
                            id++;
                        }
                    }
                }
            }

            return (idToSymbol, symbolToId);
        }
    }
}

[ExportCompilerDeveloperSdkLspServiceFactory(typeof(IOperationVisualizerCache))]
[Shared]
sealed class IOperationVisualizerCacheFactory : AbstractCompilerDeveloperSdkLspServiceFactory
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationVisualizerCacheFactory()
    {
    }

    public override IOperationVisualizerCache CreateILspService(CompilerDeveloperSdkLspServices lspServices) => new();
}
