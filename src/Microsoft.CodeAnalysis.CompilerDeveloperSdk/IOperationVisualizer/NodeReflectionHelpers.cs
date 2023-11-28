using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

static partial class NodeReflectionHelpers
{
    internal readonly record struct IOperationReflectionInformation(
        ImmutableArray<(string Name, bool IsArray)> OperationChildNames,
        IReadOnlyDictionary<string, Func<object?, object?>> OperationPropertyAccessors,
        ImmutableArray<string> NonOperationPropertyNames,
        IReadOnlyDictionary<string, Func<object?, object?>> NonOperationPropertyAccessors);
    private static readonly ConcurrentDictionary<Type, IOperationReflectionInformation> s_ioperationReflectionInformation = new();
    private static readonly Type IOperationType = typeof(IOperation);
    private static readonly Type ImmutableArrayType = typeof(ImmutableArray<>);

    internal static IOperationReflectionInformation GetIOperationReflectionInformation(IOperation o)
    {
        return s_ioperationReflectionInformation.GetOrAdd(o.GetType(), t =>
        {
            var operationChildNames = ImmutableArray.CreateBuilder<(string Name, bool IsArray)>();
            var operationPropertyAccessors = new Dictionary<string, Func<object?, object?>>();
            var nonOperationPropertyNames = ImmutableArray.CreateBuilder<string>();
            var nonOperationPropertyAccessors = new Dictionary<string, Func<object?, object?>>();

            foreach (var property in t.GetProperties())
            {
#pragma warning disable CS0618 // IOperation.Children is obsolete
                if (property.Name is nameof(IOperation.Parent) or nameof(IOperation.ChildOperations) or nameof(IOperation.Children) or nameof(IOperation.SemanticModel)) continue;
#pragma warning restore CS0618

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
                    else
                    {
                        nonOperationPropertyNames.Add(property.Name);
                        nonOperationPropertyAccessors.Add(property.Name, property.GetValue);
                    }
                }
                else
                {
                    nonOperationPropertyNames.Add(property.Name);
                    nonOperationPropertyAccessors.Add(property.Name, property.GetValue);
                }
            }

            return new(operationChildNames.ToImmutable(), operationPropertyAccessors, nonOperationPropertyNames.ToImmutable(), nonOperationPropertyAccessors);
        });
    }

    private static readonly ConcurrentDictionary<Type, string> s_operationTypeToInterfaceName = new();

    internal static string GetIOperationInterfaceName(IOperation o)
    {
        return s_operationTypeToInterfaceName.GetOrAdd(o.GetType(), t =>
        {
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
        });
    }

    internal readonly record struct SymbolReflectionInformation(IReadOnlyDictionary<string, Func<object?, object?>> PropertyAccessors);
    private static readonly ConcurrentDictionary<Type, SymbolReflectionInformation> s_symbolReflectionInformation = new();
    private static readonly Type ISymbolType = typeof(ISymbol);

    internal static SymbolReflectionInformation GetSymbolReflectionInformation(ISymbol symbol)
    {
        return s_symbolReflectionInformation.GetOrAdd(symbol.GetType(), t =>
        {
            var propertyAccessors = new Dictionary<string, Func<object?, object?>>();

            var interfaces = t.GetInterfaces();

            foreach (var @interface in interfaces)
            {
                if (!@interface.IsAssignableTo(ISymbolType))
                {
                    continue;
                }

                var map = t.GetInterfaceMap(@interface);
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    var interfaceMethod = map.InterfaceMethods[i];
                    var implementationMethod = map.TargetMethods[i];

                    var propertyMatch = GetPropertyName().Match(interfaceMethod.Name);

                    if (propertyMatch.Success && propertyMatch.Groups.TryGetValue("name", out var name))
                    {
                        _ = propertyAccessors.TryAdd(name.Value, @this => implementationMethod.Invoke(@this, null));
                    }
                }
            }

            return new(propertyAccessors);
        });
    }

    [GeneratedRegex(@"^get_(?<name>.+)$")]
    private static partial Regex GetPropertyName();
}
