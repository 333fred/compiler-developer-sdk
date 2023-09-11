using System.Collections.Immutable;
using System.Composition;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationTreeRequest : ITextDocumentParams
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "parentSymbolId")]
    public int? ParentSymbolId { get; init; }
    [DataMember(Name = "parentIOperationId")]
    public int? ParentIOperationId { get; init; }
}

[DataContract]
sealed class IOperationTreeResponse
{
    [DataMember(Name = "nodes")]
    public required ImmutableArray<IOperationTreeNode> Nodes { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IOperationTreeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationTree)]
sealed class IOperationTreeService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<IOperationTreeRequest, IOperationTreeResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationTreeService()
    {
    }

    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;

    public override TextDocumentIdentifier GetTextDocumentIdentifier(IOperationTreeRequest request) => request.TextDocument;

    public override async Task<IOperationTreeResponse> HandleRequestAsync(IOperationTreeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();

        var document = context.GetRequiredDocument();

        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken);
        var text = await document.GetTextAsync(cancellationToken);

        return request.ParentSymbolId switch
        {
            null or -1 => new IOperationTreeResponse
            {
                Nodes = ImmutableArray.Create(cacheEntry.IdToSymbol[0].ToTreeNode(text))
            },
            int parentId when cacheEntry.IdToSymbol.TryGetValue(parentId, out var parentItem) =>
                new IOperationTreeResponse
                {
                    Nodes = parentItem.ChildIds.Select(i => cacheEntry.IdToSymbol[i].ToTreeNode(text)).ToImmutableArray()
                },
            _ => throw new ArgumentException("Invalid parent symbol id", nameof(request))
        };
    }
}
