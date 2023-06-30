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

[DataContract]
sealed class SyntaxNodeParentResponse
{
    [DataMember(Name = "parent")]
    public SyntaxTreeNode? Parent { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeParentService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxNodeParent)]
class SyntaxNodeParentService : ICompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxNodeParentRequest, SyntaxNodeParentResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxNodeParentService()
    {
    }

    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(SyntaxNodeParentRequest request) => request.TextDocument;

    public async Task<SyntaxNodeParentResponse> HandleRequestAsync(SyntaxNodeParentRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SyntaxVisualizerCache>();
        var document = context.GetRequiredDocument();

        if (!cache.TryGetCachedEntry(document, out var entry))
        {
            return new SyntaxNodeParentResponse();
        }

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(text is not null);

        var child = entry.NodeMap[request.ChildId];

        return child.Node is CompilationUnitSyntax
            ? new SyntaxNodeParentResponse()
            : new SyntaxNodeParentResponse { Parent = SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(child.Parent, text!, entry.IdMap[child.Parent]) };
    }
}
