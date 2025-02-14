using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

sealed class SymbolDetailVisualizerCache : ICacheEntry<SymbolDetailVisualizerCache>
{
    private readonly object _lock = new();
    private readonly Dictionary<int, ISymbol> _idToSymbolMap = [];
    private readonly Dictionary<ISymbol, int> _symbolToIdMap = new(SymbolEqualityComparer.Default);

    public static Task<SymbolDetailVisualizerCache> CreateFromDocument(Document document, CancellationToken ct)
    {
        return Task.FromResult(new SymbolDetailVisualizerCache());
    }

    public int GetId(ISymbol symbol)
    {
        lock (_lock)
        {
            if (_symbolToIdMap.TryGetValue(symbol, out var id))
            {
                return id;
            }

            id = _idToSymbolMap.Count;
            _idToSymbolMap[id] = symbol;
            _symbolToIdMap[symbol] = id;
            return id;
        }
    }

    public bool TryGetSymbol(int id, [NotNullWhen(true)] out ISymbol? symbol)
    {
        lock (_lock)
        {
            return _idToSymbolMap.TryGetValue(id, out symbol);
        }
    }
}

sealed class SymbolDetailVisualizerDocumentCache : VisualizerCache<SymbolDetailVisualizerCache>;
