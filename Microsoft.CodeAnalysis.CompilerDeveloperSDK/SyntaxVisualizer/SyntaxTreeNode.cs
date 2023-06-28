using System.Runtime.Serialization;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSDK;

[DataContract]
record SyntaxTreeNode
{
    [DataMember(Name = "nodeType")]
    public required SymbolAndKind NodeType { get; set; }
    [DataMember(Name = "range")]
    public required LSP.Range Range { get; set; }
    [DataMember(Name = "hasChildren")]
    public required bool HasChildren { get; set; }
    [DataMember(Name = "id")]
    public required int Id { get; set; }
}

[DataContract]
struct SymbolAndKind
{
    public static SymbolAndKind Null { get; } = new() { Symbol = "<null>", SymbolKind = null };

    [DataMember(Name = "symbol")]
    public required string Symbol { get; set; }
    [DataMember(Name = "symbolKind")]
    public string? SymbolKind { get; set; }

    public override readonly string ToString()
    {
        return $$"""SymbolAndString { {{nameof(Symbol)}} = "{Symbol}", {{nameof(SymbolKind)}} = {{(SymbolKind == null ? "null" : @$"""{SymbolKind}""")}} }""";
    }
}
