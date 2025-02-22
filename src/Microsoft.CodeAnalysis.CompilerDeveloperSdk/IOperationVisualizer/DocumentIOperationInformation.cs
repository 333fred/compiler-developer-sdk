using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

sealed record DocumentIOperationInformation(IReadOnlyDictionary<int, SyntaxAndSymbol> IdToSymbol, IReadOnlyDictionary<SyntaxNode, int> SyntaxNodeToId) : ICacheEntry<DocumentIOperationInformation>
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

            idToSymbol.Add(0, new(node, Symbol: null, ParentId: -1, SymbolId: 0, ChildIds: []));
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
            if (node is MemberDeclarationSyntax memberDeclaration and not (GlobalStatementSyntax or BasePropertyDeclarationSyntax or EventDeclarationSyntax)
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

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            HandleProperty(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            HandleProperty(node);
        }

        private void HandleProperty(BasePropertyDeclarationSyntax node)
        {
            int previousParentId = _parentId;
            var propertySymbol = (IPropertySymbol?)semanticModel.GetDeclaredSymbol(node);

            if (propertySymbol is null)
            {
                Debug.Fail("How?");
                return;
            }

            StoreInfo(propertySymbol, node);

            if (node is PropertyDeclarationSyntax { ExpressionBody: { } expressionBody })
            {
                Debug.Assert(propertySymbol.GetMethod is not null);
                StoreInfo(propertySymbol.GetMethod, expressionBody);
            }

            if (node.AccessorList is { } accessorList)
            {
                var propertyId = _parentId;
                foreach (var accessor in accessorList.Accessors)
                {
                    if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                    {
                        Debug.Assert(propertySymbol.GetMethod is not null);
                        StoreInfo(propertySymbol.GetMethod, accessor);
                    }
                    else
                    {
                        Debug.Assert(accessor.Kind() is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration);
                        Debug.Assert(propertySymbol.SetMethod is not null);
                        StoreInfo(propertySymbol.SetMethod, accessor);
                    }

                    _parentId = propertyId;
                }
            }

            _parentId = previousParentId;
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            int previousParentId = _parentId;
            var eventSymbol = (IEventSymbol?)semanticModel.GetDeclaredSymbol(node);

            if (eventSymbol is null)
            {
                Debug.Fail("How?");
                return;
            }

            StoreInfo(eventSymbol, node);

            if (node.AccessorList is { } accessorList)
            {
                var propertyId = _parentId;
                foreach (var accessor in accessorList.Accessors)
                {
                    if (accessor.IsKind(SyntaxKind.AddAccessorDeclaration))
                    {
                        Debug.Assert(eventSymbol.AddMethod is not null);
                        StoreInfo(eventSymbol.AddMethod, accessor);
                    }
                    else
                    {
                        Debug.Assert(accessor.IsKind(SyntaxKind.RemoveAccessorDeclaration));
                        Debug.Assert(eventSymbol.RemoveMethod is not null);
                        StoreInfo(eventSymbol.RemoveMethod, accessor);
                    }

                    _parentId = propertyId;
                }
            }

            _parentId = previousParentId;
        }

        private void StoreInfo(ISymbol symbol, SyntaxNode syntaxNode)
        {
            var symbolId = _nextId++;
            var syntaxAndSymbol = new SyntaxAndSymbol(syntaxNode, symbol, _parentId, symbolId, []);

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
    private static readonly StrongBox<IOperationChildren> s_empty = new(new(ImmutableDictionary<IOperation, (int, string?, bool)>.Empty, ImmutableDictionary<int, (IOperation, string?)>.Empty, []));
    private static readonly ImmutableArray<int> s_noAttributes = [0];

    private StrongBox<IOperationChildren>? _operationToId = null;

    public IOperationTreeNode ToTreeNode(SourceText text)
    {
        var location = text.Lines.GetLinePositionSpan(Syntax.Span);
        return IOperationTreeNode.SymbolToTreeItem(Symbol, HasIOperationChildren, location, SymbolId, ChildIds);
    }

    public bool HasIOperationChildren
        => _operationToId != null
           ? _operationToId != s_empty
           : Syntax switch
           {
               VariableDeclaratorSyntax { Initializer: not null } => true,
               BaseMethodDeclarationSyntax { Body: not null } or BaseMethodDeclarationSyntax { ExpressionBody: not null } => true,
               MemberDeclarationSyntax { AttributeLists.Count: > 0 } => true,
               AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
               ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax } => true,
               CompilationUnitSyntax when Symbol is IMethodSymbol { Name: WellKnownMemberNames.TopLevelStatementsEntryPointMethodName } => true,
               _ => false
           };

    public async ValueTask<IOperationChildren> GetOrComputeIOperationChildrenAsync(Document document, CancellationToken cancellationToken)
    {
        if (_operationToId != null)
        {
            return _operationToId.Value;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        Debug.Assert(model is not null);

        var syntax = AdjustSyntaxNode(Syntax, Symbol);

        var iopRoot = model.GetOperation(syntax, cancellationToken);

        var attrLists = Syntax switch
        {
            MemberDeclarationSyntax { AttributeLists: { Count: > 0 } a } => a,
            AccessorDeclarationSyntax { AttributeLists: { Count: > 0 } a } => a,
            ArrowExpressionClauseSyntax => default, // Can't have an attribute on the arrow expression itself, it'll be on the property
            _ => default,
        };

        if (iopRoot is null && attrLists.Count == 0)
        {
            // Don't care about multiple assignments here, it's always the same shared static empty value
            _operationToId = s_empty;
            return _operationToId.Value;
        }

        var operationToId = new Dictionary<IOperation, (int, string?, bool)>();
        var idToOperation = new Dictionary<int, (IOperation, string?)>();
        var stack = new Stack<(IOperation Operation, string? ParentName, bool ParentIsArray)>();
        var currentId = 0;

        ImmutableArray<int> roots;
        if (iopRoot is not null)
        {
            walkIOperationChildren(iopRoot);
            roots = s_noAttributes;
        }
        else
        {
            roots = [];
        }


        if (attrLists.Count > 0)
        {
            var rootsBuilder = roots.ToBuilder();
            foreach (var attrList in attrLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrOperation = model.GetOperation(attr, cancellationToken);
                    if (attrOperation is null)
                    {
                        continue;
                    }

                    rootsBuilder.Add(currentId);
                    walkIOperationChildren(attrOperation);
                }
            }
            roots = rootsBuilder.ToImmutable();
        }

        Interlocked.CompareExchange(ref _operationToId, new(new(operationToId, idToOperation, roots)), null);

        return _operationToId.Value;

        static SyntaxNode AdjustSyntaxNode(SyntaxNode syntaxNode, ISymbol? symbol)
            => syntaxNode switch
            {
                AccessorDeclarationSyntax accessor => (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody!,
                ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax, Expression: var expression } => expression,
                // Top-level statements use the CompilationUnitSyntax as the operation root
                GlobalStatementSyntax { Parent: CompilationUnitSyntax compilationUnit } when symbol is IMethodSymbol { Name: WellKnownMemberNames.TopLevelStatementsEntryPointMethodName } => compilationUnit,
                _ => syntaxNode
            };

        void walkIOperationChildren(IOperation root)
        {
            stack.Push((root, null, false));

            while (stack.TryPop(out var current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                operationToId.Add(current.Operation, (currentId, current.ParentName, current.ParentIsArray));
                idToOperation.Add(currentId++, (current.Operation, current.ParentName));
                var reflectionInfo = NodeReflectionHelpers.GetIOperationReflectionInformation(current.Operation);
                foreach (var (name, accessor) in reflectionInfo.OperationPropertyAccessors)
                {
                    switch (accessor(current.Operation))
                    {
                        case IOperation childOperation:
                            stack.Push((childOperation, name, false));
                            break;
                        case IEnumerable<IOperation> childOperations:
                            foreach (var childOperation in childOperations)
                            {
                                stack.Push((childOperation, name, true));
                            }
                            break;
                    }
                }
            }
        }
    }
}

readonly record struct IOperationChildren(IReadOnlyDictionary<IOperation, (int Id, string? ParentName, bool IsArray)> IOperationToId, IReadOnlyDictionary<int, (IOperation Operation, string? ParentName)> IdToIOperation, ImmutableArray<int> Roots);

sealed class IOperationVisualizerCache : VisualizerCache<DocumentIOperationInformation>;

[ExportCompilerDeveloperSdkLspServiceFactory(typeof(IOperationVisualizerCache)), Shared]
[method: ImportingConstructor]
[method: Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
sealed class IOperationVisualizerCacheFactory() : AbstractCompilerDeveloperSdkLspServiceFactory
{
    public override AbstractCompilerDeveloperSdkLspService CreateILspService(CompilerDeveloperSdkLspServices lspServices) => new IOperationVisualizerCache();
}
