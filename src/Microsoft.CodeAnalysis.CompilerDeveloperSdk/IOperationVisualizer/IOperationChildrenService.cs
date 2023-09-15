using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationChildrenRequest : ITextDocumentParams
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "parentSymbolId")]
    public int ParentSymbolId { get; init; }
    [DataMember(Name = "parentIOperationId")]
    public int? ParentIOperationId { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IOperationChildrenService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationChildren)]
sealed class IOperationChildrenService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<IOperationChildrenRequest, IOperationTreeResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationChildrenService()
    {
    }

    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;
    public override TextDocumentIdentifier GetTextDocumentIdentifier(IOperationChildrenRequest request) => request.TextDocument;

    public override async Task<IOperationTreeResponse> HandleRequestAsync(IOperationChildrenRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();

        var document = context.GetRequiredDocument();

        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken);
        var text = await document.GetTextAsync(cancellationToken);

        var parentSymbol = cacheEntry.IdToSymbol[request.ParentSymbolId];

        var (operationToId, idToOperation) = await parentSymbol.GetOrComputeIOperationChildrenAsync(document, cancellationToken);

        if (operationToId == null)
        {
            return new() { Nodes = ImmutableArray.Create<IOperationTreeNode>() };
        }

        Debug.Assert(idToOperation != null);

        if (request.ParentIOperationId is int parentId)
        {
            if (idToOperation.TryGetValue(parentId, out var parentOperation))
            {
                var children = parentOperation.ChildOperations.Select(o => o.ToTreeNode(request.ParentSymbolId, operationToId[o], text)).ToImmutableArray();
                return new() { Nodes = children };
            }
            else
            {
                return new() { Nodes = ImmutableArray.Create<IOperationTreeNode>() };
            }
        }
        else
        {
            return new() { Nodes = ImmutableArray.Create(idToOperation[0].ToTreeNode(request.ParentSymbolId, 0, text)) };
        }

    }
}