using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxTreeRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "parentNodeId"), JsonPropertyName("parentNodeId")]
    public int? ParentNodeId { get; init; }
}

[DataContract]
sealed class SyntaxTreeResponse
{
    [DataMember(Name = "nodes"), JsonPropertyName("nodes")]
    public required ImmutableArray<SyntaxTreeNode> Nodes { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxTreeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxTree)]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class SyntaxTreeService() : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxTreeRequest, SyntaxTreeResponse>
{
    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => true;

    public override Uri GetTextDocumentIdentifier(SyntaxTreeRequest request) => request.TextDocument.Uri;

    public override async Task<SyntaxTreeResponse> HandleRequestAsync(SyntaxTreeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SyntaxVisualizerCache>();

        var document = context.GetRequiredDocument();

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken);

        return request.ParentNodeId switch
        {
            null => new SyntaxTreeResponse { Nodes = [SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(cacheEntry.NodeMap[0], text, nodeId: 0)] },
            int parentId when cacheEntry.NodeMap.TryGetValue(parentId, out var parentItem) =>
                new SyntaxTreeResponse
                {
                    Nodes = [.. parentItem.GetChildren().Select(s => SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(s, text, cacheEntry.IdMap[s]))]
                },
            _ => new SyntaxTreeResponse { Nodes = [] }
        };
    }
}
