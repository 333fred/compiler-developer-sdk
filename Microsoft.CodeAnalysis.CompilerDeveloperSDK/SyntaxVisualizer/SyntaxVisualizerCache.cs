using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSDK;

class SyntaxVisualizerCache : ResolveCache<SyntaxVisualizerCacheEntry>, ICompilerDeveloperSdkLspService
{
    public SyntaxVisualizerCache() : base(3)
    {
    }
}

record SyntaxVisualizerCacheEntry(IReadOnlyDictionary<int, SyntaxNodeOrTokenOrTrivia> NodeMap, IReadOnlyDictionary<SyntaxNodeOrTokenOrTrivia, int> IdMap);
