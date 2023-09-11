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
    [DataMember(Name = "hasChildren")]
    public required bool HasChildren { get; init; }
    [DataMember(Name = "symbolId")]
    public required int SymbolId { get; init; }
    [DataMember(Name = "ioperationId")]
    public required int? IOperationId { get; init; }

    public static IOperationTreeNode SymbolToTreeItem(ISymbol? symbol, LinePositionSpan originalLocation, int symbolId, ImmutableArray<int> childIds)
    {
        if (symbol == null)
        {
            // Root node
            return new()
            {
                NodeType = new() { Symbol = "Root", SymbolKind = "None" },
                HasChildren = !childIds.IsEmpty,
                SymbolId = symbolId,
                Range = ProtocolConversions.LinePositionToRange(originalLocation),
                IOperationId = null,
            };
        }

        return new()
        {
            NodeType = new() { Symbol = symbol.Name, SymbolKind = symbol.GetKindString() },
            HasChildren = !childIds.IsEmpty,
            SymbolId = symbolId,
            Range = ProtocolConversions.LinePositionToRange(originalLocation),
            IOperationId = null,
        };
    }
}
