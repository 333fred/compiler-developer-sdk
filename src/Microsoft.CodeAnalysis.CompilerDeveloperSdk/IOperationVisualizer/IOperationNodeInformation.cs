using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
internal readonly record struct OperationChild(
    [property: DataMember(Name = "name"), JsonPropertyName("name")]
    string Name,
    [property: DataMember(Name = "isArray"), JsonPropertyName("isArray")]
    bool IsArray,
    [property: DataMember(Name = "isPresent"), JsonPropertyName("isPresent")]
    bool IsPresent);

[DataContract]
sealed class IOperationNodeInformation
{
    [DataMember(Name = "parentInfo"), JsonPropertyName("parentInfo")]
    public required OperationChild? ParentInfo { get; init; }
    [DataMember(Name = "ioperationId"), JsonPropertyName("ioperationId")]
    public required int IOperationId { get; init; }
    [DataMember(Name = "operationChildrenInfo"), JsonPropertyName("operationChildrenInfo")]
    public required ImmutableArray<OperationChild> OperationChildrenInfo { get; init; }

    public static IOperationNodeInformation FromOperation(IOperation operation, int operationId, OperationChild? parentInfo, out IReadOnlyDictionary<string, string> properties)
    {
        var reflectionInfo = NodeReflectionHelpers.GetIOperationReflectionInformation(operation);

        var propertiesDictionary = new Dictionary<string, string>(reflectionInfo.NonOperationPropertyAccessors.Count);
        foreach (var name in reflectionInfo.NonOperationPropertyNames)
        {
            var value = reflectionInfo.NonOperationPropertyAccessors[name](operation);
            propertiesDictionary.Add(name, value?.ToString() ?? "<null>");
        }

        properties = propertiesDictionary;

        var operationChildrenNames = ImmutableArray.CreateBuilder<OperationChild>(reflectionInfo.OperationChildNames.Length);
        foreach (var (name, isArray) in reflectionInfo.OperationChildNames)
        {
            var value = reflectionInfo.OperationPropertyAccessors[name](operation);
            operationChildrenNames.Add(new(name, isArray, value is not null));
        }

        return new()
        {
            ParentInfo = parentInfo,
            IOperationId = operationId,
            OperationChildrenInfo = operationChildrenNames.ToImmutable(),
        };
    }

    public static ImmutableArray<IOperation> GetOperationChildrenForName(string name, IOperation parent)
    {
        var reflectionInfo = NodeReflectionHelpers.GetIOperationReflectionInformation(parent);

        var accessor = reflectionInfo.OperationPropertyAccessors[name];

        return accessor(parent) switch
        {
            IOperation operation => [operation],
            IEnumerable<IOperation> operations => [.. operations],
            var x => throw new InvalidOperationException($"Property {name} on {parent.GetType()} is not an IOperation or IEnumerable<IOperation>: {x?.GetType().ToString() ?? "<null>"}")
        };
    }
}
