using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.Text;

using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxTreeNode
{
    [DataMember(Name = "nodeType")]
    public required SymbolAndKind NodeType { get; set; }
    [DataMember(Name = "range")]
    public required LSP.Range Range { get; set; }
    [DataMember(Name = "hasChildren")]
    public required bool HasChildren { get; set; }
    [DataMember(Name = "nodeId")]
    public required int NodeId { get; set; }

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
record struct SymbolAndKind
{
    public static SymbolAndKind Null { get; } = new() { Symbol = "<null>", SymbolKind = "Unknown" };

    [DataMember(Name = "symbol")]
    public required string Symbol { get; set; }
    [DataMember(Name = "symbolKind")]
    public required string SymbolKind { get; set; }

    public override readonly string ToString()
    {
        return $$"""SymbolAndString { {{nameof(Symbol)}} = "{Symbol}", {{nameof(SymbolKind)}} = {{(SymbolKind == null ? "null" : @$"""{SymbolKind}""")}} }""";
    }
}
