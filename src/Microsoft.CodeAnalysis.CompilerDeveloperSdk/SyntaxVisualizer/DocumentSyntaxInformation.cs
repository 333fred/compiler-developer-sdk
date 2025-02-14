using System.Composition;
using System.Diagnostics;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

sealed record DocumentSyntaxInformation(IReadOnlyDictionary<int, SyntaxNodeOrTokenOrTrivia> NodeMap, IReadOnlyDictionary<SyntaxNodeOrTokenOrTrivia, int> IdMap) : ICacheEntry<DocumentSyntaxInformation>
{
    public static async Task<DocumentSyntaxInformation> CreateFromDocument(Document document, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        Debug.Assert(root != null);
        buildIdMap(root, out var nodeMap, out var idMap);
        return new DocumentSyntaxInformation(nodeMap, idMap);

        static void buildIdMap(SyntaxNode syntaxNode, out Dictionary<int, SyntaxNodeOrTokenOrTrivia> nodeMap, out Dictionary<SyntaxNodeOrTokenOrTrivia, int> idMap)
        {
            // First time we've seen this file. Build the map
            int id = 0;
            nodeMap = [];
            idMap = [];
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
                        mapTrivia(ref id, token.LeadingTrivia, isLeading: true, nodeMap, idMap);
                    }

                    if (token.HasTrailingTrivia)
                    {
                        mapTrivia(ref id, token.TrailingTrivia, isLeading: false, nodeMap, idMap);
                    }
                }
            }

            static void mapTrivia(ref int id, SyntaxTriviaList triviaList, bool isLeading, Dictionary<int, SyntaxNodeOrTokenOrTrivia> nodeMap, Dictionary<SyntaxNodeOrTokenOrTrivia, int> idMap)
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

sealed class SyntaxVisualizerCache : VisualizerCache<DocumentSyntaxInformation>;

[ExportCompilerDeveloperSdkLspServiceFactory(typeof(SyntaxVisualizerCache)), Shared]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class SyntaxVisualizerCacheFactory() : AbstractCompilerDeveloperSdkLspServiceFactory {
    public override AbstractCompilerDeveloperSdkLspService CreateILspService(CompilerDeveloperSdkLspServices lspServices) => new SyntaxVisualizerCache();
}
