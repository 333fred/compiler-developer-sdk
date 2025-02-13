using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxNodeParentRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "childId"), JsonPropertyName("childId")]
    public required int ChildId { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeParentService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxNodeParent)]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class SyntaxNodeParentService() : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxNodeParentRequest, NodeParentResponse<SyntaxTreeNode>>
{
    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => true;

    public override Uri GetTextDocumentIdentifier(SyntaxNodeParentRequest request) => request.TextDocument.Uri;

    public override async Task<NodeParentResponse<SyntaxTreeNode>> HandleRequestAsync(SyntaxNodeParentRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SyntaxVisualizerCache>();
        var document = context.GetRequiredDocument();

        if (!cache.TryGetCachedEntry(document, out var entry))
        {
            return new();
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        var child = entry.NodeMap[request.ChildId];

        return child.Node is CompilationUnitSyntax
            ? new()
            : new() { Parent = SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(child.Parent, text!, entry.IdMap[child.Parent]) };
    }
}
