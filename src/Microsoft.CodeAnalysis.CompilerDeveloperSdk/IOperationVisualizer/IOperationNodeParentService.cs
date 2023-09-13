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

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IOperationNodeParentService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationNodeParent)]
sealed class IOperationNodeParentService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<IOperationNodeParentRequest, NodeParentResponse<IOperationTreeNode>>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationNodeParentService()
    {
    }

    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;
    public override TextDocumentIdentifier GetTextDocumentIdentifier(IOperationNodeParentRequest request) => request.TextDocument;

    public override async Task<NodeParentResponse<IOperationTreeNode>> HandleRequestAsync(IOperationNodeParentRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();
        var document = context.GetRequiredDocument();

        if (!cache.TryGetCachedEntry(document, out var entry))
        {
            return new();
        }

        var child = entry.IdToSymbol[request.ChildSymbolId];

        if (child.ParentId == -1)
        {
            // Root node, no parent
            return new();
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        return new() { Parent = entry.IdToSymbol[child.ParentId].ToTreeNode(text) };
    }
}
