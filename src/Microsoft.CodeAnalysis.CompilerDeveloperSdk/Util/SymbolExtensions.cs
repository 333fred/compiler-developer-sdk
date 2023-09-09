namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

public static class SymbolExtensions
{
    public static string GetKindString(this ISymbol symbol) => symbol switch
    {
        IAliasSymbol { Target: var t } => GetKindString(t),
        ITypeSymbol { TypeKind: var t } => t.ToString(),
        IMethodSymbol { MethodKind: MethodKind.BuiltinOperator or MethodKind.UserDefinedOperator } => "Operator",
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.Destructor or MethodKind.StaticConstructor } => "Constructor",
        IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } => "EnumMember",
        { Kind: var k } => k.ToString(),
    };
}
