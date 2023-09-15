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
    [DataMember(Name = "Properties")]
    public required IReadOnlyDictionary<string, string> Properties { get; init; }

    public static IOperationNodeInformation FromOperation(IOperation operation, int operationId)
    {
        var operationType = operation.GetType();
        var reflectionInfo = s_reflectionInformation.GetOrAdd(operationType, CreateReflectionInformation);

        var properties = new Dictionary<string, string>(reflectionInfo.NonOperationPropertyAccessors.Count);
        foreach (var name in reflectionInfo.NonOperationPropertyNames)
        {
            var value = reflectionInfo.NonOperationPropertyAccessors[name](operation);
            properties.Add(name, value?.ToString() ?? "<null>");
        }

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
            Properties = properties,
        };
    }

    private static ReflectionInformation CreateReflectionInformation(Type t)
    {
        var operationChildNames = ImmutableArray.CreateBuilder<(string Name, bool IsArray)>();
        var operationPropertyAccessors = new Dictionary<string, Func<object?, object?>>();
        var nonOperationPropertyNames = ImmutableArray.CreateBuilder<string>();
        var nonOperationPropertyAccessors = new Dictionary<string, Func<object?, object?>>();

        foreach (var property in t.GetProperties())
        {
            if (property.Name == nameof(IOperation.Parent)) continue;

            if (property.PropertyType.IsAssignableTo(IOperationType))
            {
                operationChildNames.Add((property.Name, false));
                operationPropertyAccessors.Add(property.Name, property.GetValue);
            }
            else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == ImmutableArrayType)
            {
                var elementType = property.PropertyType.GetGenericArguments()[0];
                if (elementType.IsAssignableTo(IOperationType))
                {
                    operationChildNames.Add((property.Name, true));
                    operationPropertyAccessors.Add(property.Name, property.GetValue);
                }
            }
            else
            {
                nonOperationPropertyNames.Add(property.Name);
                nonOperationPropertyAccessors.Add(property.Name, property.GetValue);
            }
        }

        return new(operationChildNames.ToImmutable(), operationPropertyAccessors, nonOperationPropertyNames.ToImmutable(), nonOperationPropertyAccessors);
    }

    public static ImmutableArray<IOperation> GetOperationChildrenForName(string name, IOperation parent)
    {
        var parentType = parent.GetType();
        var reflectionInfo = s_reflectionInformation.GetOrAdd(parentType, CreateReflectionInformation);

        var accessor = reflectionInfo.OperationPropertyAccessors[name];

        return accessor(parent) switch
        {
            IOperation operation => ImmutableArray.Create(operation),
            IEnumerable<IOperation> operations => operations.ToImmutableArray(),
            var x => throw new InvalidOperationException($"Property {name} on {parentType} is not an IOperation or IEnumerable<IOperation>: {x?.GetType().ToString() ?? "<null>"}")
        };
    }
}
