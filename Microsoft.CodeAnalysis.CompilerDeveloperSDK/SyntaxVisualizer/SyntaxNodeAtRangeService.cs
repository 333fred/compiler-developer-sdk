using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxNodeAtRangeRequest
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "range")]
    public required LSP.Range Range { get; init; }
}

[DataContract]
sealed class SyntaxNodeAtRangeResponse
{
    [DataMember(Name = "node")]
    public required SyntaxTreeNode? Node { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeAtRangeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxNodeAtRange)]
sealed class SyntaxNodeAtRangeService : ICompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxNodeAtRangeRequest, SyntaxNodeAtRangeResponse>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxNodeAtRangeService()
    {
    }

    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(SyntaxNodeAtRangeRequest request) => request.TextDocument;

    public async Task<SyntaxNodeAtRangeResponse> HandleRequestAsync(SyntaxNodeAtRangeRequest request, RequestContext context, CancellationToken cancellationToken)
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

        return new SyntaxNodeAtRangeResponse { Node = SyntaxTreeNode.NodeOrTokenOrTriviaToTreeItem(element, text!, cacheEntry.IdMap[element]) };
    }
}
