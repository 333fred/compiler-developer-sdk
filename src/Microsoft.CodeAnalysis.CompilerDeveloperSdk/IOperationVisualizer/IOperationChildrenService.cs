using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationChildrenRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "parentSymbolId"), JsonPropertyName("parentSymbolId")]
    public int ParentSymbolId { get; init; }
    [DataMember(Name = "parentIOperationId"), JsonPropertyName("parentIOperationId")]
    public int? ParentIOperationId { get; init; }
    [DataMember(Name = "parentIOperationPropertyName"), JsonPropertyName("parentIOperationPropertyName")]
    public string? ParentIOperationPropertyName { get; init; }
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
    public override Uri GetTextDocumentIdentifier(IOperationChildrenRequest request) => request.TextDocument.Uri;

    public override async Task<IOperationTreeResponse> HandleRequestAsync(IOperationChildrenRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();

        var document = context.GetRequiredDocument();

        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken);
        var text = await document.GetTextAsync(cancellationToken);

        var parentSymbol = cacheEntry.IdToSymbol[request.ParentSymbolId];

        var (operationToId, idToOperation, roots) = await parentSymbol.GetOrComputeIOperationChildrenAsync(document, cancellationToken);

        if (operationToId == null)
        {
            return new() { Nodes = ImmutableArray.Create<IOperationTreeNode>() };
        }

        Debug.Assert(idToOperation != null);

        if (request.ParentIOperationId is int parentId)
        {
            if (idToOperation.TryGetValue(parentId, out var parentOperation))
            {
                Debug.Assert(request.ParentIOperationPropertyName != null);
                var operationChildren = IOperationNodeInformation.GetOperationChildrenForName(request.ParentIOperationPropertyName, parentOperation.Operation);
                return new()
                {
                    Nodes = operationChildren.Select(o =>
                    {
                        var (id, parentName, parentIsArray) = operationToId[o];
                        Debug.Assert(parentName is not null);
                        var parentInfo = new OperationChild(parentName, parentIsArray, IsPresent: true);
                        return o.ToTreeNode(request.ParentSymbolId, id, parentInfo, text);
                    }).ToImmutableArray()
                };
            }
            else
            {
                return new() { Nodes = ImmutableArray.Create<IOperationTreeNode>() };
            }
        }
        else
        {
            return new()
            {
                Nodes = roots.Select(r =>
                {
                    (IOperation operation, string? parentName) = idToOperation[r];
                    return operation.ToTreeNode(request.ParentSymbolId, r, parentInfo: null, text);
                }).ToImmutableArray()
            };
        }

    }
}
