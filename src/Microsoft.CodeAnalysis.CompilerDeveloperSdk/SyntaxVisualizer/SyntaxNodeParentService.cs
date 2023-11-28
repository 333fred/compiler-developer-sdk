using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxNodeParentRequest
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "childId")]
    public required int ChildId { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeParentService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxNodeParent)]
sealed class SyntaxNodeParentService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxNodeParentRequest, NodeParentResponse<SyntaxTreeNode>>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxNodeParentService()
    {
    }

    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => true;

    public override TextDocumentIdentifier GetTextDocumentIdentifier(SyntaxNodeParentRequest request) => request.TextDocument;

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
