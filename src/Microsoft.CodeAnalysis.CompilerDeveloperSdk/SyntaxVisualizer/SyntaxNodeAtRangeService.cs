using System.Composition;
using System.Diagnostics;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using SyntaxNodeAtRangeResponse = Microsoft.CodeAnalysis.CompilerDeveloperSdk.NodeAtRangeResponse<Microsoft.CodeAnalysis.CompilerDeveloperSdk.SyntaxTreeNode>;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeAtRangeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxNodeAtRange)]
sealed class SyntaxNodeAtRangeService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<NodeAtRangeRequest, SyntaxNodeAtRangeResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxNodeAtRangeService()
    {
    }

    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => true;

    public override TextDocumentIdentifier GetTextDocumentIdentifier(NodeAtRangeRequest request) => request.TextDocument;

    public override async Task<SyntaxNodeAtRangeResponse> HandleRequestAsync(NodeAtRangeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SyntaxVisualizerCache>();
        var document = context.GetRequiredDocument();
        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(root != null);

        // Find the nearest token or node to the given position. Don't include trivia, it's likely not what
        // the user wanted.
        var span = ProtocolConversions.RangeToTextSpan(request.Range, text);
        SyntaxNodeOrToken element = span.Length == 0 ? root.FindToken(span.Start) : root.FindNode(span);

        return new() { Node = SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(element, text!, cacheEntry.IdMap[element]) };
    }
}
