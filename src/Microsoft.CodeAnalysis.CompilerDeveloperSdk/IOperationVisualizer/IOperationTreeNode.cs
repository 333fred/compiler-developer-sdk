using System.Collections.Immutable;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.Text;

using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationTreeNode
{
    [DataMember(Name = "nodeType")]
    public required SymbolAndKind NodeType { get; init; }
    [DataMember(Name = "range")]
    public required LSP.Range Range { get; init; }
    [DataMember(Name = "hasSymbolChildren")]
    public required bool HasSymbolChildren { get; init; }
    [DataMember(Name = "hasIOperationChildren")]
    public required bool HasIOperationChildren { get; init; }
    [DataMember(Name = "symbolId")]
    public required int SymbolId { get; init; }
    [DataMember(Name = "ioperationId")]
    public required int? IOperationId { get; init; }

    public static IOperationTreeNode SymbolToTreeItem(ISymbol? symbol, bool hasIOperationChildren, LinePositionSpan originalLocation, int symbolId, ImmutableArray<int> childIds)
    {
        if (symbol == null)
        {
            // Root node
            return new()
            {
                NodeType = new() { Symbol = "Root", SymbolKind = "None" },
                HasSymbolChildren = !childIds.IsEmpty,
                HasIOperationChildren = hasIOperationChildren,
                SymbolId = symbolId,
                Range = ProtocolConversions.LinePositionToRange(originalLocation),
                IOperationId = null,
            };
        }

        return new()
        {
            NodeType = new() { Symbol = symbol.Name, SymbolKind = symbol.GetKindString() },
            HasSymbolChildren = !childIds.IsEmpty,
            HasIOperationChildren = hasIOperationChildren,
            SymbolId = symbolId,
            Range = ProtocolConversions.LinePositionToRange(originalLocation),
            IOperationId = null,
        };
    }
}

static class IOperationExtensions
{
    public static IOperationTreeNode ToTreeNode(this IOperation operation, int containingSymbolId, int ioperationId, SourceText text)
    {
        var operationSpan = text.Lines.GetLinePositionSpan(operation.Syntax.Span);

        return new()
        {
            NodeType = new() { Symbol = operation.Kind.ToString(), SymbolKind = "class" },
            HasSymbolChildren = false,
            HasIOperationChildren = operation.ChildOperations.Any(),
            SymbolId = containingSymbolId,
            Range = ProtocolConversions.LinePositionToRange(operationSpan),
            IOperationId = ioperationId,
        };
    }
}
