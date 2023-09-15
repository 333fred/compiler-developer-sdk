using System.Composition;
using System.Diagnostics;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IOperationNodeAtRangeService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IOperationNodeAtRange)]
sealed class IOperationNodeAtRangeService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<NodeAtRangeRequest, NodeAtRangeResponse<IOperationTreeNode>?>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationNodeAtRangeService()
    {
    }

    public override bool RequiresLSPSolution => true;

    public override bool MutatesSolutionState => false;

    public override TextDocumentIdentifier GetTextDocumentIdentifier(NodeAtRangeRequest request) => request.TextDocument;

    public override async Task<NodeAtRangeResponse<IOperationTreeNode>?> HandleRequestAsync(NodeAtRangeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<IOperationVisualizerCache>();
        var document = context.GetRequiredDocument();
        var cacheEntry = await cache.GetOrAddCachedEntry(document, cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(root != null);

        var span = ProtocolConversions.RangeToTextSpan(request.Range, text);
        var element = root.FindNode(span);

        // Now that we have the syntax of the specific location the user requested, walk upwards until we find the enclosing member declaration,
        // so that we can get the enclosing symbol for the current spot.
        while (element != null)
        {
            if (tryGetCacheEntry(element, out var entry)
                // If we didn't come up through a later declarator, but instead came up through one of the other nodes (such as the type or field initializer),
                // we assume the user will want the first field decl
                || (element is FieldDeclarationSyntax { Declaration.Variables: { } variables } && tryGetCacheEntry(variables[0], out entry)))
            {
                // TODO: Now walk in to find the actual IOperation node in question, if it exists.
                return new() { Node = entry?.ToTreeNode(text) };
            }

            element = element.Parent;
        }

        // Couldn't find anything? Should be impossible, we should always have a node for the root.
        Debug.Fail($"Couldn't find a node for the given range {span}");
        return null;

        bool tryGetCacheEntry(SyntaxNode syntaxNode, out SyntaxAndSymbol? entry)
        {
            if (cacheEntry.SyntaxNodeToId.TryGetValue(syntaxNode, out var id))
            {
                entry = cacheEntry.IdToSymbol[id];
                return true;
            }

            entry = null;
            return false;
        }
    }
}
