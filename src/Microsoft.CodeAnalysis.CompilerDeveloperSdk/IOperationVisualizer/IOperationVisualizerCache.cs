using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.Text;

sealed class IOperationVisualizerCache : AbstractCompilerDeveloperSdkLspService
{
    private readonly ConditionalWeakTable<Document, DocumentIOperationInformation> _cache = new();

    public bool TryGetCachedEntry(Document document, [NotNullWhen(true)] out DocumentIOperationInformation? entry)
    {
        return _cache.TryGetValue(document, out entry);
    }

    private void SetCachedEntry(Document document, DocumentIOperationInformation entry)
    {
        _cache.Add(document, entry);
    }

    public async Task<DocumentIOperationInformation> GetOrAddCachedEntry(Document document, CancellationToken cancellationToken)
    {
        if (!TryGetCachedEntry(document, out var entry))
        {
            entry = await DocumentIOperationInformation.CreateFromDocument(document, cancellationToken).ConfigureAwait(false);
            SetCachedEntry(document, entry);
        }

        return entry;
    }
}

sealed record DocumentIOperationInformation(IReadOnlyDictionary<int, SyntaxAndSymbol> IdToSymbol, IReadOnlyDictionary<SyntaxNode, int> SyntaxNodeToId)
{
    public static async Task<DocumentIOperationInformation> CreateFromDocument(Document document, CancellationToken ct)
    {
        var (idToSymbol, syntaxToId) = await BuildIdMap(document, ct);
        return new DocumentIOperationInformation(idToSymbol, syntaxToId);

        static async Task<(Dictionary<int, SyntaxAndSymbol>, Dictionary<SyntaxNode, int>)> BuildIdMap(Document document, CancellationToken ct)
        {
            // First time we've seen this file. Build the map
            var idToSymbol = new Dictionary<int, SyntaxAndSymbol>();
            var syntaxToId = new Dictionary<SyntaxNode, int>();

            var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);

            Debug.Assert(root != null);
            Debug.Assert(model != null);

            var walker = new SyntaxWalker(model, idToSymbol, syntaxToId);
            walker.StartVisit(root);

            return (idToSymbol, syntaxToId);
        }
    }

    private sealed class SyntaxWalker(SemanticModel semanticModel, Dictionary<int, SyntaxAndSymbol> idToSymbol, Dictionary<SyntaxNode, int> syntaxToId) : CSharpSyntaxWalker
    {
        private int _nextId = 1;
        private int _parentId = 0;

        public void StartVisit(SyntaxNode node)
        {
            Debug.Assert(node is CompilationUnitSyntax);

            idToSymbol.Add(0, new(node, Symbol: null, ParentId: -1, SymbolId: 0, ChildIds: ImmutableArray<int>.Empty));
            syntaxToId.Add(node, 0);

            if (semanticModel.GetDeclaredSymbol(node) is { } tlsSymbol)
            {
                StoreInfo(tlsSymbol, node);
                _parentId = 0;
            }

            Visit(node);
        }

        public override void Visit(SyntaxNode? node)
        {
            int previousParentId = _parentId;
            if (node is MemberDeclarationSyntax memberDeclaration and not GlobalStatementSyntax
                && semanticModel.GetDeclaredSymbol(memberDeclaration) is { } declaredSymbol)
            {
                StoreInfo(declaredSymbol, node);
            }

            base.Visit(node);
            _parentId = previousParentId;
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            int previousParentId = _parentId;
            if (node is { Parent.Parent: FieldDeclarationSyntax fieldDeclaration } && semanticModel.GetDeclaredSymbol(node) is { } declaredSymbol)
            {
                StoreInfo(declaredSymbol, node);
            }

            base.VisitVariableDeclarator(node);

            _parentId = previousParentId;
        }

        public override void VisitGlobalStatement(GlobalStatementSyntax node)
        {
            // We don't visit inside top level statements
            return;
        }

        private void StoreInfo(ISymbol symbol, SyntaxNode syntaxNode)
        {
            var symbolId = _nextId++;
            var syntaxAndSymbol = new SyntaxAndSymbol(syntaxNode, symbol, _parentId, symbolId, ImmutableArray<int>.Empty);

            idToSymbol[symbolId] = syntaxAndSymbol;
            var parent = idToSymbol[_parentId];
            idToSymbol[_parentId] = parent with { ChildIds = parent.ChildIds.Add(symbolId) };
            if (syntaxNode is CompilationUnitSyntax && symbol is IMethodSymbol { Name: WellKnownMemberNames.TopLevelStatementsEntryPointMethodName })
            {
                // Replace the root node with the entry point method symbol in the syntax map. We assume that if a user clicks in a top level statement,
                // they'd rather tree reveal that, not the root node.
                syntaxToId[syntaxNode] = symbolId;
            }
            else
            {
                syntaxToId.Add(syntaxNode, symbolId);
            }

            _parentId = symbolId;
        }
    }
}

record SyntaxAndSymbol(SyntaxNode Syntax, ISymbol? Symbol, int ParentId, int SymbolId, ImmutableArray<int> ChildIds)
{
    private static readonly StrongBox<(IReadOnlyDictionary<IOperation, int>, IReadOnlyDictionary<int, IOperation>)> s_empty = new((ImmutableDictionary<IOperation, int>.Empty, ImmutableDictionary<int, IOperation>.Empty));

    private StrongBox<(IReadOnlyDictionary<IOperation, int>, IReadOnlyDictionary<int, IOperation>)>? _operationToId = null;

    public IOperationTreeNode ToTreeNode(SourceText text)
    {
        var location = text.Lines.GetLinePositionSpan(Syntax.Span);
        return IOperationTreeNode.SymbolToTreeItem(Symbol, HasIOperationChildren, location, SymbolId, ChildIds);
    }

    public bool HasIOperationChildren
        => _operationToId != null
           ? _operationToId == s_empty
           : Syntax switch
           {
               VariableDeclaratorSyntax { Initializer: not null } => true,
               BaseMethodDeclarationSyntax { Body: not null } or BaseMethodDeclarationSyntax { ExpressionBody: not null } => true,
               MemberDeclarationSyntax { AttributeLists.Count: > 0 } => true,
               // TODO: Properties should have getter/setter children in the tree, and have the iop children under each respective node
               _ => false
           };

    public async ValueTask<(IReadOnlyDictionary<IOperation, int> IOperationToId, IReadOnlyDictionary<int, IOperation> IdToIOperation)> GetOrComputeIOperationChildrenAsync(Document document, CancellationToken cancellationToken)
    {
        if (_operationToId != null)
        {
            return _operationToId.Value;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(model is not null);
        var iopRoot = model.GetOperation(Syntax, cancellationToken);

        if (iopRoot is null)
        {
            // Don't care about multiple assignments here, it's always the same shared static empty value
            _operationToId = s_empty;
            return _operationToId.Value;
        }

        var operationToId = new Dictionary<IOperation, int>();
        var idToOperation = new Dictionary<int, IOperation>();
        var stack = new Stack<IOperation>();
        var currentId = 0;
        stack.Push(iopRoot);

        while (stack.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            operationToId.Add(current, currentId);
            idToOperation.Add(currentId++, current);
            foreach (var child in current.ChildOperations.Reverse())
            {
                stack.Push(child);
            }
        }

        Interlocked.CompareExchange(ref _operationToId, new((operationToId, idToOperation)), null);

        return _operationToId.Value;
    }
}

[ExportCompilerDeveloperSdkLspServiceFactory(typeof(IOperationVisualizerCache))]
[Shared]
sealed class IOperationVisualizerCacheFactory : AbstractCompilerDeveloperSdkLspServiceFactory
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IOperationVisualizerCacheFactory()
    {
    }

    public override IOperationVisualizerCache CreateILspService(CompilerDeveloperSdkLspServices lspServices) => new();
}
