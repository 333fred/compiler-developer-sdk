using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationNodeInformation
{
    [DataContract]
    internal readonly record struct OperationChild(
        [property: DataMember(Name = "name")]
        string Name,
        [property: DataMember(Name = "isArray")]
        bool IsArray,
        [property: DataMember(Name = "isPresent")]
        bool IsPresent);
    private readonly record struct ReflectionInformation(
        ImmutableArray<(string Name, bool IsArray)> OperationChildNames,
        IReadOnlyDictionary<string, Func<object?, object?>> OperationPropertyAccessors,
        ImmutableArray<string> NonOperationPropertyNames,
        IReadOnlyDictionary<string, Func<object?, object?>> NonOperationPropertyAccessors);
    private static readonly ConcurrentDictionary<Type, ReflectionInformation> s_reflectionInformation = new();
    private static readonly Type IOperationType = typeof(IOperation);
    private static readonly Type ImmutableArrayType = typeof(ImmutableArray<>);

    [DataMember(Name = "ioperationId")]
    public required int IOperationId { get; init; }
    [DataMember(Name = "operationChildrenInfo")]
    public required ImmutableArray<OperationChild> OperationChildrenInfo { get; init; }

    public static IOperationNodeInformation FromOperation(IOperation operation, int operationId, out IReadOnlyDictionary<string, string> properties)
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
            IOperation operation => ImmutableArray.Create(operation),
            IEnumerable<IOperation> operations => operations.ToImmutableArray(),
            var x => throw new InvalidOperationException($"Property {name} on {parent.GetType()} is not an IOperation or IEnumerable<IOperation>: {x?.GetType().ToString() ?? "<null>"}")
        };
    }
}
