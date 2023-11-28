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
    [DataMember(Name = "ioperationInfo")]
    public required IOperationNodeInformation? IOperationInfo { get; init; }
    [DataMember(Name = "properties")]
    public required IReadOnlyDictionary<string, string>? Properties { get; init; }

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
                IOperationInfo = null,
                Properties = null
            };
        }

        var reflectionInformation = NodeReflectionHelpers.GetSymbolReflectionInformation(symbol);
        Dictionary<string, string> properties = new(reflectionInformation.PropertyAccessors.Count);
        foreach (var (name, accessor) in reflectionInformation.PropertyAccessors)
        {
            var value = accessor(symbol);
            properties.Add(name, value?.ToString() ?? "<null>");
        }

        return new()
        {
            NodeType = new() { Symbol = symbol.Name, SymbolKind = symbol.GetKindString() },
            HasSymbolChildren = !childIds.IsEmpty,
            HasIOperationChildren = hasIOperationChildren,
            SymbolId = symbolId,
            Range = ProtocolConversions.LinePositionToRange(originalLocation),
            IOperationInfo = null,
            Properties = properties,
        };
    }
}

static class IOperationExtensions
{
    public static IOperationTreeNode ToTreeNode(this IOperation operation, int containingSymbolId, int ioperationId, OperationChild? parentInfo, SourceText text)
    {
        var operationSpan = text.Lines.GetLinePositionSpan(operation.Syntax.Span);

        var nodeName = NodeReflectionHelpers.GetIOperationInterfaceName(operation);

        return new()
        {
            NodeType = new() { Symbol = nodeName, SymbolKind = "Class" },
            HasSymbolChildren = false,
            HasIOperationChildren = operation.ChildOperations.Any(),
            SymbolId = containingSymbolId,
            Range = ProtocolConversions.LinePositionToRange(operationSpan),
            IOperationInfo = IOperationNodeInformation.FromOperation(operation, ioperationId, parentInfo, out var properties),
            Properties = properties,
        };
    }
}
