using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxTreeRequest : ITextDocumentParams
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    public int? ParentNodeId { get; init; }
}

sealed class SyntaxTreeResponse
{
    [DataMember(Name = "syntaxTree")]
    public required ImmutableArray<SyntaxTreeNode> SyntaxTree { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxTreeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxTree)]
sealed class SyntaxTreeService : ICompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxTreeRequest, SyntaxTreeResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxTreeService()
    {
    }

    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(SyntaxTreeRequest request) => request.TextDocument;

    public async Task<SyntaxTreeResponse> HandleRequestAsync(SyntaxTreeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SyntaxVisualizerCache>();

        var document = context.GetRequiredDocument();

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken);

        return request.ParentNodeId switch
        {
            null => new SyntaxTreeResponse { SyntaxTree = ImmutableArray.Create(SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(cacheEntry.NodeMap[0], text!, nodeId: 0)) },
            int parentId when cacheEntry.NodeMap.TryGetValue(parentId, out var parentItem) => new SyntaxTreeResponse { SyntaxTree = parentItem.GetChildren().Select(s => SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(s, text!, cacheEntry.IdMap[s])).ToImmutableArray() },
            _ => new SyntaxTreeResponse { SyntaxTree = ImmutableArray<SyntaxTreeNode>.Empty }
        };
    }
}
