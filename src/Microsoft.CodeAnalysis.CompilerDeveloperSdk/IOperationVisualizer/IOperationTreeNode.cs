using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
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
            };
        }

        return new()
        {
            NodeType = new() { Symbol = symbol.Name, SymbolKind = symbol.GetKindString() },
            HasSymbolChildren = !childIds.IsEmpty,
            HasIOperationChildren = hasIOperationChildren,
            SymbolId = symbolId,
            Range = ProtocolConversions.LinePositionToRange(originalLocation),
            IOperationInfo = null,
        };
    }
}

static class IOperationExtensions
{
    private static readonly ConcurrentDictionary<Type, string> s_operationTypeToInterfaceName = new();
    private static readonly Type IOperationType = typeof(IOperation);

    public static IOperationTreeNode ToTreeNode(this IOperation operation, int containingSymbolId, int ioperationId, SourceText text)
    {
        var operationSpan = text.Lines.GetLinePositionSpan(operation.Syntax.Span);

        var nodeName = s_operationTypeToInterfaceName.GetOrAdd(operation.GetType(), GetIOperationInterfaceName);

        return new()
        {
            NodeType = new() { Symbol = nodeName, SymbolKind = "Class" },
            HasSymbolChildren = false,
            HasIOperationChildren = operation.ChildOperations.Any(),
            SymbolId = containingSymbolId,
            Range = ProtocolConversions.LinePositionToRange(operationSpan),
            IOperationInfo = IOperationNodeInformation.FromOperation(operation, ioperationId)
        };

        static string GetIOperationInterfaceName(Type t)
        {
            Debug.Assert(t.IsAssignableTo(IOperationType));

            foreach (var @interface in t.GetInterfaces())
            {
                // Find the interface that is assignable to IOperation but is not IOperation itself
                if (@interface.IsAssignableTo(IOperationType) && @interface != IOperationType)
                {
                    return @interface.Name;
                }
            }

            // This is NoneOperation
            Debug.Assert(t.Name == "NoneOperation");
            return "NoneOperation";
        }
    }
}
