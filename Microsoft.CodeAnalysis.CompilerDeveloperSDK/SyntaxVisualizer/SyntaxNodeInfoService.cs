using System.Collections.Immutable;
using System.Composition;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxNodeInfoRequest
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "node")]
    public required SyntaxTreeNode Node { get; init; }
}

[DataContract]
sealed class SyntaxNodeInfoResponse
{
    [DataMember(Name = "nodeType")]
    public SymbolAndKind NodeType { get; set; }
    [DataMember(Name = "nodeSyntaxKind")]
    public required string NodeSyntaxKind { get; set; }
    [DataMember(Name = "semanticClassification")]
    public string? SemanticClassification { get; set; }
    [DataMember(Name = "nodeSymbolInfo")]
    public NodeSymbolInfo? NodeSymbolInfo { get; set; }
    [DataMember(Name = "nodeTypeInfo")]
    public NodeTypeInfo? NodeTypeInfo { get; set; }
    [DataMember(Name = "nodeDeclaredSymbol")]
    public SymbolAndKind NodeDeclaredSymbol { get; set; } = SymbolAndKind.Null;
    [DataMember(Name = "properties")]
    public required ImmutableDictionary<string, string> Properties { get; set; }
}

[DataContract]
sealed class NodeSymbolInfo
{
    [DataMember(Name = "symbol")]
    public SymbolAndKind Symbol { get; set; }
    [DataMember(Name = "candidateReason")]
    public required string CandidateReason { get; set; }
    [DataMember(Name = "candidateSymbols")]
    public ImmutableArray<SymbolAndKind> CandidateSymbols { get; set; }
}

[DataContract]
sealed class NodeTypeInfo
{
    [DataMember(Name = "type")]
    public SymbolAndKind Type { get; set; }
    [DataMember(Name = "convertedType")]
    public SymbolAndKind ConvertedType { get; set; }
    [DataMember(Name = "conversion")]
    public string? Conversion { get; set; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeInfoService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxTreeNodeInfo)]
sealed class SyntaxNodeInfoService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxNodeInfoRequest, SyntaxNodeInfoResponse?>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxNodeInfoService()
    {
    }

    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => true;

    public override TextDocumentIdentifier GetTextDocumentIdentifier(SyntaxNodeInfoRequest request) => request.TextDocument;

    public override async Task<SyntaxNodeInfoResponse?> HandleRequestAsync(SyntaxNodeInfoRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SyntaxVisualizerCache>();
        var document = context.GetRequiredDocument();
        if (!cache.TryGetCachedEntry(document, out var entry))
        {
            return null;
        }

        var item = entry.NodeMap[request.Node.NodeId];
        var itemSpan = item.GetSpan();
        var classification = await Classifier.GetClassifiedSpansAsync(document, itemSpan, cancellationToken);

        var response = new SyntaxNodeInfoResponse
        {
            NodeType = new() { Symbol = item.GetUnderlyingType().ToString(), SymbolKind = item.Node is null ? "Struct" : "Class" },
            NodeSyntaxKind = item.Kind(),
            Properties = item.GetPublicProperties(),
            SemanticClassification = classification.FirstOrDefault().ClassificationType,
        };

        await PopulateNodeInformation(item.Node);

        return response;

        async Task PopulateNodeInformation(SyntaxNode? node)
        {
            if (node is null) return;

            var model = await document.GetSemanticModelAsync(cancellationToken);
            if (model is null) return;

            var typeInfo = model.GetTypeInfo(node, cancellationToken: cancellationToken);
            if (typeInfo.Type is not null || typeInfo.ConvertedType is not null)
            {
                response.NodeTypeInfo = new NodeTypeInfo
                {
                    Type = GetSymbolAndKind(typeInfo.Type, model, node.SpanStart),
                    ConvertedType = GetSymbolAndKind(typeInfo.ConvertedType, model, node.SpanStart),
                    Conversion = model.GetConversion(node, cancellationToken: cancellationToken).ToString()
                };
            }

            var symbolInfo = model.GetSymbolInfo(node, cancellationToken: cancellationToken);
            if (symbolInfo.Symbol is not null || !symbolInfo.CandidateSymbols.IsDefaultOrEmpty)
            {
                response.NodeSymbolInfo = new NodeSymbolInfo
                {
                    Symbol = GetSymbolAndKind(symbolInfo.Symbol, model, node.SpanStart),
                    CandidateSymbols = symbolInfo.CandidateSymbols.Select(s => GetSymbolAndKind(s, model, node.SpanStart)).ToImmutableArray(),
                    CandidateReason = symbolInfo.CandidateReason.ToString()
                };
            }

            response.NodeDeclaredSymbol = GetSymbolAndKind(model.GetDeclaredSymbol(node, cancellationToken: cancellationToken), model, node.SpanStart);
        }

        static SymbolAndKind GetSymbolAndKind(ISymbol? symbol, SemanticModel model, int position)
        {
            return symbol is null
                ? SymbolAndKind.Null
                : new() { Symbol = symbol.ToMinimalDisplayString(model, position), SymbolKind = GetKindString(symbol) };
        }

        static string GetKindString(ISymbol symbol) => symbol switch
        {
            IAliasSymbol { Target: var t } => GetKindString(t),
            ITypeSymbol { TypeKind: var t } => t.ToString(),
            IMethodSymbol { MethodKind: MethodKind.BuiltinOperator or MethodKind.UserDefinedOperator } => "Operator",
            IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.Destructor or MethodKind.StaticConstructor } => "Constructor",
            IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } => "EnumMember",
            { Kind: var k } => k.ToString(),
        };
    }
}
