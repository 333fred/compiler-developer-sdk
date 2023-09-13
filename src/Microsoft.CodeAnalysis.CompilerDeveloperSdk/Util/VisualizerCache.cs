using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

interface ICacheEntry<TSelf> where TSelf : ICacheEntry<TSelf>
{
    static abstract Task<TSelf> CreateFromDocument(Document document, CancellationToken ct);
}

sealed class VisualizerCache<TCacheEntry> : AbstractCompilerDeveloperSdkLspService where TCacheEntry : class, ICacheEntry<TCacheEntry>
{
    private readonly ConditionalWeakTable<Document, TCacheEntry> _cache = new();

    public bool TryGetCachedEntry(Document document, [NotNullWhen(true)] out TCacheEntry? entry) => _cache.TryGetValue(document, out entry);

    public async Task<TCacheEntry> GetOrAddCachedEntry(Document document, CancellationToken cancellationToken)
    {
        if (!TryGetCachedEntry(document, out var entry))
        {
            entry = await TCacheEntry.CreateFromDocument(document, cancellationToken);
            entry = _cache.TryAdd(document, entry) ? entry : _cache.GetValue(document, _ => throw new InvalidOperationException("We've held onto a strong reference to the document, if this fails it's a GC bug."));
        }

        return entry;
    }
}

abstract class VisualizerCacheFactory<TCacheEntry> : AbstractCompilerDeveloperSdkLspServiceFactory where TCacheEntry : class, ICacheEntry<TCacheEntry>
{
    public override VisualizerCache<TCacheEntry> CreateILspService(CompilerDeveloperSdkLspServices lspServices) => new();
}
