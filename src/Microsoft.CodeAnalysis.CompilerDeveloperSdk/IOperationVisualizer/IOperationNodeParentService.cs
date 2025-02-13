using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationNodeParentRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "childSymbolId"), JsonPropertyName("childSymbolId")]
    public required int ChildSymbolId { get; init; }
    [DataMember(Name = "childIOperationId"), JsonPropertyName("childIOperationId")]
    public required int? ChildIOperationId { get; init; }
}

[DataContract]
sealed class IOperationParentResponse : NodeParentResponse<IOperationTreeNode>
{
    [DataMember(Name = "parentOperationPropertyName"), JsonPropertyName("parentOperationPropertyName")]
    public string? ParentOperationPropertyName { get; init; }
    [DataMember(Name = "isArray"), JsonPropertyName("isArray")]
    public bool IsArray { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IOperationNodeParentService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationNodeParent)]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class IOperationNodeParentService() : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<IOperationNodeParentRequest, IOperationParentResponse>
{
    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;
    public override Uri GetTextDocumentIdentifier(IOperationNodeParentRequest request) => request.TextDocument.Uri;

    public override async Task<IOperationParentResponse> HandleRequestAsync(IOperationNodeParentRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();
        var document = context.GetRequiredDocument();

        if (!cache.TryGetCachedEntry(document, out var entry))
        {
            return new();
        }

        var childSymbolInfo = entry.IdToSymbol[request.ChildSymbolId];

        if (childSymbolInfo.ParentId == -1)
        {
            // Root node, no parent
            return new();
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        if (request.ChildIOperationId is not -1 and int childId)
        {
            var childIOperationInfo = await childSymbolInfo.GetOrComputeIOperationChildrenAsync(document, cancellationToken).ConfigureAwait(false);
            var (childOperation, _) = childIOperationInfo.IdToIOperation[childId];

            if (childOperation.Parent is { } parentOperation)
            {
                var (parentId, parentName, parentIsArray) = childIOperationInfo.IOperationToId[parentOperation];
                var parentInfo = parentName != null
                    ? new OperationChild(parentName, parentIsArray, IsPresent: true)
                    : (OperationChild?)null;
                return new()
                {
                    Parent = parentOperation.ToTreeNode(request.ChildSymbolId, parentId, parentInfo, text),
                    ParentOperationPropertyName = parentName,
                    IsArray = parentIsArray
                };
            }

            return new() { Parent = childSymbolInfo.ToTreeNode(text) };
        }

        return new() { Parent = entry.IdToSymbol[childSymbolInfo.ParentId].ToTreeNode(text) };
    }
}
