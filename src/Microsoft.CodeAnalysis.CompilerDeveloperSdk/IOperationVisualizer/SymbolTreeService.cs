using System.Collections.Immutable;
using System.Composition;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SymbolTreeRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "parentSymbolId"), JsonPropertyName("parentSymbolId")]
    public int? ParentSymbolId { get; init; }
}

[DataContract]
sealed class IOperationTreeResponse
{
    [DataMember(Name = "nodes"), JsonPropertyName("nodes")]
    public required ImmutableArray<IOperationTreeNode> Nodes { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SymbolTreeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationTree)]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class SymbolTreeService() : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SymbolTreeRequest, IOperationTreeResponse>
{
    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;

    public override Uri GetTextDocumentIdentifier(SymbolTreeRequest request) => request.TextDocument.Uri;

    public override async Task<IOperationTreeResponse> HandleRequestAsync(SymbolTreeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();

        var document = context.GetRequiredDocument();

        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken);
        var text = await document.GetTextAsync(cancellationToken);

        return request.ParentSymbolId switch
        {
            null or -1 => new IOperationTreeResponse
            {
                Nodes = [cacheEntry.IdToSymbol[0].ToTreeNode(text)]
            },
            int parentId when cacheEntry.IdToSymbol.TryGetValue(parentId, out var parentItem) =>
                new IOperationTreeResponse
                {
                    Nodes = [.. parentItem.ChildIds.Select(i => cacheEntry.IdToSymbol[i].ToTreeNode(text))]
                },
            _ => throw new ArgumentException("Invalid parent symbol id", nameof(request))
        };
    }
}
