namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

static class Endpoints
{
    public const string SyntaxTree = "syntaxTree";
    public const string SyntaxNodeAtRange = "syntaxTree/nodeAtRange";
    public const string SyntaxNodeParent = "syntaxTree/parentNode";
    public const string SyntaxTreeNodeInfo = "syntaxTree/info";

    public const string IOperationTree = "operationTree";
    public const string IOperationChildren = "operationTree/operationChildren";
    public const string IOperationNodeAtRange = "operationTree/nodeAtRange";
    public const string IOperationNodeParent = "operationTree/parentNode";

    public const string IlForContainingSymbol = "il/containingSymbol";

    public const string SymbolDetails = "symbol/details";
}
