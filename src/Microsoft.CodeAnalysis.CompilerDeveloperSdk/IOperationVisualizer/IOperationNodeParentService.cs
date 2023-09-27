using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationNodeParentRequest
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "childSymbolId")]
    public required int ChildSymbolId { get; init; }
    [DataMember(Name = "childIOperationId")]
    public required int? ChildIOperationId { get; init; }
}

[DataContract]
sealed class IOperationParentResponse : NodeParentResponse<IOperationTreeNode>
{
    [DataMember(Name = "parentOperationPropertyName")]
    public string? ParentOperationPropertyName { get; init; }
    [DataMember(Name = "isArray")]
    public bool IsArray { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IOperationNodeParentService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationNodeParent)]
sealed class IOperationNodeParentService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<IOperationNodeParentRequest, IOperationParentResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationNodeParentService()
    {
    }

    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;
    public override TextDocumentIdentifier GetTextDocumentIdentifier(IOperationNodeParentRequest request) => request.TextDocument;

    public override async Task<IOperationParentResponse> HandleRequestAsync(IOperationNodeParentRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();
        var document = context.GetRequiredDocument();

        if (!cache.TryGetCachedEntry(document, out var entry))
        {
            return new();
        }

        var childInfo = entry.IdToSymbol[request.ChildSymbolId];

        if (childInfo.ParentId == -1)
        {
            // Root node, no parent
            return new();
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        if (request.ChildIOperationId is not -1 and int childId)
        {
            var ioperationInfo = await childInfo.GetOrComputeIOperationChildrenAsync(document, cancellationToken).ConfigureAwait(false);
            var childOperation = ioperationInfo.IdToIOperation[childId];

            if (childOperation.Parent is { } parentOperation)
            {
                var parentId = ioperationInfo.IOperationToId[parentOperation];
                var (name, isArray) = getParentName(parentOperation) ?? default;
                return new() { Parent = parentOperation.ToTreeNode(request.ChildSymbolId, parentId, text), ParentOperationPropertyName = name, IsArray = isArray };
            }

            return new() { Parent = childInfo.ToTreeNode(text) };
        }

        return new() { Parent = entry.IdToSymbol[childInfo.ParentId].ToTreeNode(text) };

        static (string name, bool isArray)? getParentName(IOperation operation)
        {
            var parent = operation.Parent;
            if (parent is null)
            {
                return null;
            }

            var reflectionInfo = NodeReflectionHelpers.GetIOperationReflectionInformation(parent);
            foreach (var (name, accessor) in reflectionInfo.OperationPropertyAccessors)
            {
                var value = accessor(parent);

                switch (value)
                {
                    case IOperation operationValue when operationValue == operation:
                        return (name, false);
                    case ICollection<IOperation> operationValues when operationValues.Contains(operation):
                        return (name, true);
                }
            }

            return null;
        }
    }
}
