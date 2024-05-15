using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.Text;

using LSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxTreeNode
{
    [DataMember(Name = "nodeType"), JsonPropertyName("nodeType")]
    public required SymbolAndKind NodeType { get; init; }
    [DataMember(Name = "range"), JsonPropertyName("range")]
    public required LSP.Range Range { get; init; }
    [DataMember(Name = "hasChildren"), JsonPropertyName("hasChildren")]
    public required bool HasChildren { get; init; }
    [DataMember(Name = "nodeId"), JsonPropertyName("nodeId")]
    public required int NodeId { get; init; }

    public static SyntaxTreeNode NodeOrTokenOrTriviaToTreeItem(SyntaxNodeOrTokenOrTrivia element, SourceText text, int nodeId)
    {
        return new SyntaxTreeNode
        {
            NodeType = new() { Symbol = element.Kind(), SymbolKind = element.Node is null ? "Struct" : "Class" },
            HasChildren = element.HasChildren(),
            NodeId = nodeId,
            Range = ProtocolConversions.TextSpanToRange(element.GetFullSpan(), text),
        };
    }
}

[DataContract]
readonly record struct SymbolAndKind
{
    public static SymbolAndKind Null { get; } = new() { Symbol = "<null>", SymbolKind = "Unknown" };

    [DataMember(Name = "symbol"), JsonPropertyName("symbol")]
    public required string Symbol { get; init; }
    [DataMember(Name = "symbolKind"), JsonPropertyName("symbolKind")]
    public required string SymbolKind { get; init; }

    public override readonly string ToString()
    {
        return $$"""SymbolAndString { {{nameof(Symbol)}} = "{Symbol}", {{nameof(SymbolKind)}} = {{(SymbolKind == null ? "null" : @$"""{SymbolKind}""")}} }""";
    }
}
