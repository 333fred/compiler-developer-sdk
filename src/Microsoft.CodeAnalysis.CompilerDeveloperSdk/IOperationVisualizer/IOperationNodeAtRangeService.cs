using System.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

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

    public override Uri GetTextDocumentIdentifier(NodeAtRangeRequest request) => request.TextDocument.Uri;

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
        var mostSpecificNode = element;

        // Now that we have the syntax of the specific location the user requested, walk upwards until we find the enclosing member declaration,
        // so that we can get the enclosing symbol for the current spot.
        while (element != null)
        {
            if (tryGetCacheEntry(element, out var entry)
                // If we didn't come up through a later declarator, but instead came up through one of the other nodes (such as the type or field initializer),
                // we assume the user will want the first field decl
                || (element is FieldDeclarationSyntax { Declaration.Variables: { } variables } && tryGetCacheEntry(variables[0], out entry)))
            {
                return new()
                {
                    Node = (await getNestedIOperation(entry, mostSpecificNode)) is ({ } op, (var id, { } parentName, var isArray))
                        ? op.ToTreeNode(cacheEntry.SyntaxNodeToId[entry.Syntax], id, new(parentName, isArray, IsPresent: true), text)
                        : entry.ToTreeNode(text)
                };
            }

            element = element.Parent;
        }

        // Couldn't find anything? Should be impossible, we should always have a node for the root.
        Debug.Fail($"Couldn't find a node for the given range {span}");
        return null;

        bool tryGetCacheEntry(SyntaxNode syntaxNode, [NotNullWhen(true)] out SyntaxAndSymbol? entry)
        {
            if (cacheEntry.SyntaxNodeToId.TryGetValue(syntaxNode, out var id))
            {
                entry = cacheEntry.IdToSymbol[id];
                return true;
            }

            entry = null;
            return false;
        }

        async Task<(IOperation? Operation, (int OperationId, string? ParentName, bool ParentIsArray))> getNestedIOperation(SyntaxAndSymbol symbol, SyntaxNode mostSpecificNode)
        {
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            Debug.Assert(model != null);
            var current = mostSpecificNode;
            var operationInformation = await symbol.GetOrComputeIOperationChildrenAsync(document, cancellationToken).ConfigureAwait(false);
            while (current != symbol.Syntax && current != null)
            {
                if (model.GetOperation(current) is { } op)
                {
                    return (op, operationInformation.IOperationToId[op]);
                }

                current = current.Parent;
            }

            return default;
        }
    }
}
