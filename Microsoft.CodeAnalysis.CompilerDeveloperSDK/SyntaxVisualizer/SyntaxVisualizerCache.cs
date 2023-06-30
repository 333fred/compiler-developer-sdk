using System.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

sealed class SyntaxVisualizerCache : ICompilerDeveloperSdkLspService
{
    private readonly ConditionalWeakTable<Document, DocumentSyntaxInformation> _cache = new();

    public bool TryGetCachedEntry(Document document, [NotNullWhen(true)] out DocumentSyntaxInformation? entry)
    {
        return _cache.TryGetValue(document, out entry);
    }

    public void SetCachedEntry(Document document, DocumentSyntaxInformation entry)
    {
        _cache.Add(document, entry);
    }

    public async Task<DocumentSyntaxInformation> GetOrAddCachedEntry(Document document, CancellationToken cancellationToken)
    {
        if (!TryGetCachedEntry(document, out var entry))
        {
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(syntaxNode != null);
            entry = DocumentSyntaxInformation.CreateFromDocument(syntaxNode);
            SetCachedEntry(document, entry);
        }

        return entry;
    }
}

sealed record DocumentSyntaxInformation(IReadOnlyDictionary<int, SyntaxNodeOrTokenOrTrivia> NodeMap, IReadOnlyDictionary<SyntaxNodeOrTokenOrTrivia, int> IdMap)
{
    public static DocumentSyntaxInformation CreateFromDocument(SyntaxNode syntaxNode)
    {
        BuildIdMap(syntaxNode, out var nodeMap, out var idMap);
        return new DocumentSyntaxInformation(nodeMap, idMap);

        static void BuildIdMap(SyntaxNode syntaxNode, out Dictionary<int, SyntaxNodeOrTokenOrTrivia> nodeMap, out Dictionary<SyntaxNodeOrTokenOrTrivia, int> idMap)
        {
            // First time we've seen this file. Build the map
            int id = 0;
            nodeMap = new Dictionary<int, SyntaxNodeOrTokenOrTrivia>();
            idMap = new Dictionary<SyntaxNodeOrTokenOrTrivia, int>();
            foreach (var nodeOrToken in syntaxNode.DescendantNodesAndTokensAndSelf())
            {
                nodeMap[id] = nodeOrToken;
                idMap[nodeOrToken] = id;
                id++;

                if (nodeOrToken.IsToken)
                {
                    var token = nodeOrToken.AsToken();
                    if (token.HasLeadingTrivia)
                    {
                        MapTrivia(ref id, token.LeadingTrivia, isLeading: true, nodeMap, idMap);
                    }

                    if (token.HasTrailingTrivia)
                    {
                        MapTrivia(ref id, token.TrailingTrivia, isLeading: false, nodeMap, idMap);
                    }
                }
            }

            static void MapTrivia(ref int id, SyntaxTriviaList triviaList, bool isLeading, Dictionary<int, SyntaxNodeOrTokenOrTrivia> nodeMap, Dictionary<SyntaxNodeOrTokenOrTrivia, int> idMap)
            {
                foreach (var element in triviaList)
                {
                    var wrappedElement = new SyntaxNodeOrTokenOrTrivia(element, isLeading);
                    nodeMap[id] = wrappedElement;
                    idMap[wrappedElement] = id;
                    id++;
                }
            }
        }
    }
}

[ExportCompilerDeveloperSdkLspServiceFactory(typeof(SyntaxVisualizerCache)), Shared]
sealed class SyntaxVisualizerCacheFactory : ICompilerDeveloperSdkLspServiceFactory
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxVisualizerCacheFactory()
    {
    }

    public ICompilerDeveloperSdkLspService CreateILspService(CompilerDeveloperSdkLspServices lspServices) => new SyntaxVisualizerCache();
}
