using System.Collections.Immutable;
using System.Composition;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SyntaxNodeInfoRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "node"), JsonPropertyName("node")]
    public required SyntaxTreeNode Node { get; init; }
}

[DataContract]
sealed class SyntaxNodeInfoResponse
{
    [DataMember(Name = "nodeType"), JsonPropertyName("nodeType")]
    public SymbolAndKind NodeType { get; set; }
    [DataMember(Name = "nodeSyntaxKind"), JsonPropertyName("nodeSyntaxKind")]
    public required string NodeSyntaxKind { get; set; }
    [DataMember(Name = "semanticClassification"), JsonPropertyName("semanticClassification")]
    public string? SemanticClassification { get; set; }
    [DataMember(Name = "nodeSymbolInfo"), JsonPropertyName("nodeSymbolInfo")]
    public NodeSymbolInfo? NodeSymbolInfo { get; set; }
    [DataMember(Name = "nodeTypeInfo"), JsonPropertyName("nodeTypeInfo")]
    public NodeTypeInfo? NodeTypeInfo { get; set; }
    [DataMember(Name = "nodeDeclaredSymbol"), JsonPropertyName("nodeDeclaredSymbol")]
    public SymbolAndKind NodeDeclaredSymbol { get; set; } = SymbolAndKind.Null;
    [DataMember(Name = "properties"), JsonPropertyName("properties")]
    public required ImmutableDictionary<string, string> Properties { get; set; }
}

[DataContract]
sealed class NodeSymbolInfo
{
    [DataMember(Name = "symbol"), JsonPropertyName("symbol")]
    public SymbolAndKind Symbol { get; set; }
    [DataMember(Name = "candidateReason"), JsonPropertyName("candidateReason")]
    public required string CandidateReason { get; set; }
    [DataMember(Name = "candidateSymbols"), JsonPropertyName("candidateSymbols")]
    public ImmutableArray<SymbolAndKind> CandidateSymbols { get; set; }
}

[DataContract]
sealed class NodeTypeInfo
{
    [DataMember(Name = "type"), JsonPropertyName("type")]
    public SymbolAndKind Type { get; set; }
    [DataMember(Name = "convertedType"), JsonPropertyName("convertedType")]
    public SymbolAndKind ConvertedType { get; set; }
    [DataMember(Name = "conversion"), JsonPropertyName("conversion")]
    public string? Conversion { get; set; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeInfoService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SyntaxTreeNodeInfo)]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class SyntaxNodeInfoService() : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxNodeInfoRequest, SyntaxNodeInfoResponse?>
{
    public override bool MutatesSolutionState => false;

    public override bool RequiresLSPSolution => true;

    public override Uri GetTextDocumentIdentifier(SyntaxNodeInfoRequest request) => request.TextDocument.Uri;

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
                    CandidateSymbols = [.. symbolInfo.CandidateSymbols.Select(s => GetSymbolAndKind(s, model, node.SpanStart))],
                    CandidateReason = symbolInfo.CandidateReason.ToString()
                };
            }

            response.NodeDeclaredSymbol = GetSymbolAndKind(model.GetDeclaredSymbol(node, cancellationToken: cancellationToken), model, node.SpanStart);
        }

        static SymbolAndKind GetSymbolAndKind(ISymbol? symbol, SemanticModel model, int position)
        {
            return symbol is null
                ? SymbolAndKind.Null
                : new() { Symbol = symbol.ToMinimalDisplayString(model, position), SymbolKind = symbol.GetKindString() };
        }
    }
}
