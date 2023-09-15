import * as vscode from 'vscode';
import { CSharpExtension } from "./csharpExtensionExports";
import * as lsp from 'vscode-languageserver-protocol';
import assert = require('node:assert');
import { Logger } from './logger';
import { NodeAtRangeRequest, NodeAtRangeResponse, NodeParentResponse, SymbolAndKind, getSymbolKindIcon } from './common';

export function createSyntaxVisualizerProvider(csharpExtension: CSharpExtension, logger: Logger): vscode.Disposable[] {
    const syntaxTreeProvider = new SyntaxTreeProvider(csharpExtension, logger);
    const treeView = vscode.window.createTreeView('syntaxTree', { treeDataProvider: syntaxTreeProvider });

    logger.log("SyntaxVisualizer views registered");

    const editorTextSelectionChangeDisposable = vscode.window.onDidChangeTextEditorSelection(async event => {
        if (treeView.visible && event.selections.length > 0 && event.textEditor.document.languageId === "csharp") {
            const firstSelection = event.selections[0];
            const range: lsp.Range = lsp.Range.create(
                lsp.Position.create(firstSelection.start.line, firstSelection.start.character),
                lsp.Position.create(firstSelection.end.line, firstSelection.end.character));
            const textDocument = lsp.TextDocumentIdentifier.create(event.textEditor.document.fileName);
            const response = await csharpExtension.experimental.sendServerRequest(syntaxNodeAtRangeRequest, { textDocument, range }, lsp.CancellationToken.None);

            if (!response || !response.node) {
                return;
            }

            syntaxTreeProvider.editorChangeCausedDataChange = true;
            await treeView.reveal({ kind: 'SyntaxTreeNodeAndFile', node: response.node, identifier: textDocument });
            if (syntaxTreeProvider.highlightEnabled) {
                const responseRange = response.node.range;
                const highlightRange = new vscode.Range(
                    new vscode.Position(responseRange.start.line, responseRange.start.character),
                    new vscode.Position(responseRange.end.line, responseRange.end.character));
                await vscode.commands.executeCommand(highlightEditorRangeCommand, highlightRange);
            }
        }
    });

    const treeViewVisibilityDisposable = treeView.onDidChangeVisibility(async (event) => {
        if (!event.visible) {
            await vscode.commands.executeCommand(clearHighlightCommand);
        }
    });

    return [treeView, editorTextSelectionChangeDisposable, treeViewVisibilityDisposable];
}

const highlightEditorRangeCommand: string = 'csharp.syntaxTreeVisualizer.highlightRange';
const clearHighlightCommand: string = 'csharp.syntaxTreeVisualizer.clearHighlight';
const highlightOnClickCommand = 'compilerDeveloperSdk.highlightOnClickSyntax';

class SyntaxTreeProvider implements vscode.TreeDataProvider<TreeNode>, vscode.Disposable {

    private readonly _wordHighlightBackground: vscode.ThemeColor;
    private readonly _wordHighlightBorder: vscode.ThemeColor;
    private readonly _decorationType: vscode.TextEditorDecorationType;
    private readonly _disposables: vscode.Disposable[];
    private readonly _onDidChangeTreeData: vscode.EventEmitter<TreeNode | undefined> = new vscode.EventEmitter<TreeNode | undefined>();
    public editorChangeCausedDataChange: boolean = false;
    public highlightEnabled: boolean = true;

    constructor(private server: CSharpExtension, private logger: Logger) {

        this._wordHighlightBackground = new vscode.ThemeColor('editor.wordHighlightBackground');
        this._wordHighlightBorder = new vscode.ThemeColor('editor.wordHighlightBorder');
        this._decorationType = vscode.window.createTextEditorDecorationType({ backgroundColor: this._wordHighlightBackground, borderColor: this._wordHighlightBorder });

        const activeEditorDisposable = vscode.window.onDidChangeActiveTextEditor(() => {
            this.logger.logDebug("Active editor changed");
            this._onDidChangeTreeData.fire(undefined);
        });

        const textDocumentChangedDisposable = vscode.workspace.onDidChangeTextDocument(async event => {
            if (event.document.languageId === "csharp") {
                this.logger.logDebug("Text document changed");
                this.editorChangeCausedDataChange = true;
                this._onDidChangeTreeData.fire(undefined);
            }
        });

        const highlightRangeCommandDisposable = vscode.commands.registerCommand(highlightEditorRangeCommand, (node) => this._highlightRange(node), this);
        const clearHighlightCommandDisposable = vscode.commands.registerCommand(clearHighlightCommand, () => this._clearHighlight(), this);
        const highlightOnClickDisposable = vscode.commands.registerCommand(highlightOnClickCommand, () => {
            this.highlightEnabled = !this.highlightEnabled;
            if (!this.highlightEnabled) {
                this._clearHighlight();
            }
        });

        this._disposables = [activeEditorDisposable, textDocumentChangedDisposable, highlightRangeCommandDisposable, clearHighlightCommandDisposable, highlightOnClickDisposable, this._onDidChangeTreeData];
    }

    readonly onDidChangeTreeData: vscode.Event<TreeNode | undefined> = this._onDidChangeTreeData.event;

    getTreeItem(element: TreeNode): vscode.TreeItem {
        let treeItem: vscode.TreeItem;
        switch (element.kind) {
            case 'SyntaxTreeNodeAndFile':
                const node = element.node;
                treeItem = new vscode.TreeItem(`${node.nodeType.symbol}`, node.hasChildren ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None);
                treeItem.description = `[${node.range.start.line}:${node.range.start.character}-${node.range.end.line}:${node.range.end.character})`;
                treeItem.command = { "title": "Highlight Range", command: highlightEditorRangeCommand, arguments: [node.range] };
                treeItem.iconPath = getSymbolKindIcon(node.nodeType.symbolKind);
                treeItem.id = `${node.nodeId}`;

                return treeItem;

            case 'PropertiesRoot':
                return new vscode.TreeItem('Properties', vscode.TreeItemCollapsibleState.Collapsed);

            case 'SyntaxNodeProperty':
                const collapsibleState = element.hasChildren
                    ? (element.category === SyntaxNodePropertyCategory.propertiesHeader ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.Expanded)
                    : vscode.TreeItemCollapsibleState.None;

                treeItem = new vscode.TreeItem(element.title, collapsibleState);
                treeItem.iconPath = element.icon;
                treeItem.description = element.description;

                return treeItem;
        }
    }

    async getChildren(element?: TreeNode): Promise<TreeNode[]> {
        if (!element || element.kind === 'SyntaxTreeNodeAndFile') {
            let identifier: lsp.TextDocumentIdentifier;
            if (!element) {
                const activeDoc = vscode.window.activeTextEditor?.document.uri.fsPath;

                if (!activeDoc || !activeDoc.endsWith(".cs")) {
                    // Not a C# file, don't display anything
                    return [];
                }

                identifier = lsp.TextDocumentIdentifier.create(activeDoc);
            }
            else {
                identifier = element.identifier;
            }

            const children = await this.server.experimental.sendServerRequest(
                syntaxTree,
                { textDocument: identifier, parentNodeId: element?.node.nodeId },
                lsp.CancellationToken.None);

            var propertiesNodeIfRequired: TreeNode[] = element ? [{ kind: 'PropertiesRoot', node: element.node, identifier }] : [];

            if (!children || !Array.isArray(children.nodes)) {
                return propertiesNodeIfRequired;
            }

            return propertiesNodeIfRequired.concat(children.nodes.map(node => { return { kind: 'SyntaxTreeNodeAndFile', node, identifier }; }));
        }
        else if (element.kind == 'PropertiesRoot') {
            let info: SyntaxNodeInfoResponse;
            try {
                info = await this.server.experimental.sendServerRequest(syntaxNodeInfoRequest, { textDocument: element.identifier, node: element.node }, lsp.CancellationToken.None);
            }
            catch (e) {
                console.log(`Error getting syntax node info: ${e}`);
                return [];
            }

            let categories: SyntaxNodeProperty[] = [
                leafNode('Node Type:', info, info.nodeType.symbol, info.nodeType.symbolKind),
                leafNode('SyntaxKind:', info, info.nodeSyntaxKind, "EnumMember"),
            ];

            if (info.semanticClassification) {
                categories.push(leafNode('Semantic Classification', info, info.semanticClassification));
            }

            if (info.nodeTypeInfo) {
                categories.push({ kind: 'SyntaxNodeProperty', category: SyntaxNodePropertyCategory.typeInfoHeader, title: 'Type Info', hasChildren: true, info });
            }
            else {
                categories.push({ kind: 'SyntaxNodeProperty', category: SyntaxNodePropertyCategory.typeInfoHeader, title: 'Type Info:', description: '<null>', hasChildren: false, info });
            }

            if (info.nodeSymbolInfo) {
                categories.push({ kind: 'SyntaxNodeProperty', category: SyntaxNodePropertyCategory.symbolInfoHeader, title: 'Symbol Info', hasChildren: true, info });
            }
            else {
                categories.push({ kind: 'SyntaxNodeProperty', category: SyntaxNodePropertyCategory.symbolInfoHeader, title: 'Symbol Info:', description: '<null>', hasChildren: false, info });
            }

            categories.push({
                kind: 'SyntaxNodeProperty',
                category: SyntaxNodePropertyCategory.declaredSymbolHeader,
                title: 'Declared Symbol:',
                description: info.nodeDeclaredSymbol.symbol,
                icon: info.nodeDeclaredSymbol.symbolKind
                    ? getSymbolKindIcon(info.nodeDeclaredSymbol.symbolKind)
                    : undefined,
                hasChildren: false,
                info
            });

            categories.push({
                kind: 'SyntaxNodeProperty',
                category: SyntaxNodePropertyCategory.propertiesHeader,
                title: 'Additional Properties',
                hasChildren: Object.keys(info.properties).length !== 0,
                info
            });

            return categories;
        }
        else {
            switch (element.category) {
                case SyntaxNodePropertyCategory.declaredSymbolHeader:
                case SyntaxNodePropertyCategory.leafNode:
                    return [];

                case SyntaxNodePropertyCategory.typeInfoHeader:
                    assert(element.info.nodeTypeInfo);
                    return [
                        leafNode('Type:', element.info, element.info.nodeTypeInfo.type.symbol, element.info.nodeTypeInfo.type.symbolKind),
                        leafNode('ConvertedType:', element.info, element.info.nodeTypeInfo.convertedType.symbol, element.info.nodeTypeInfo.convertedType.symbolKind),
                        leafNode('Conversion:', element.info, element.info.nodeTypeInfo.conversion)
                    ];

                case SyntaxNodePropertyCategory.symbolInfoHeader:
                    assert(element.info.nodeSymbolInfo);
                    let symbolInfoNodes = [
                        leafNode('Symbol:', element.info, element.info.nodeSymbolInfo.symbol.symbol, element.info.nodeSymbolInfo.symbol.symbolKind),
                        leafNode('Candidate Reason:', element.info, element.info.nodeSymbolInfo.candidateReason)
                    ];

                    if (element.info.nodeSymbolInfo.candidateSymbols.length > 0) {
                        symbolInfoNodes.push({ kind: 'SyntaxNodeProperty', category: SyntaxNodePropertyCategory.candidateSymbolsHeader, title: 'Candidate Symbols', hasChildren: true, info: element.info });
                    }
                    else {
                        symbolInfoNodes.push(leafNode('Candidate Symbols', element.info, 'None'));
                    }

                    return symbolInfoNodes;

                case SyntaxNodePropertyCategory.candidateSymbolsHeader:
                    assert(element.info.nodeSymbolInfo!.candidateSymbols.length > 0);
                    return element.info.nodeSymbolInfo!.candidateSymbols.map(s => leafNode(s.symbol, element.info, undefined, s.symbolKind));

                case SyntaxNodePropertyCategory.propertiesHeader:
                    let properties: SyntaxNodeProperty[] = [];
                    for (const [key, value] of Object.entries(element.info.properties).sort((a, b) => a[0].localeCompare(b[0]))) {
                        properties.push(leafNode(key, element.info, value));
                    }

                    return properties;
            }
        }
    }

    async getParent(element: SyntaxTreeNodeAndFile): Promise<SyntaxTreeNodeAndFile | undefined> {
        const response = await this.server.experimental.sendServerRequest(syntaxNodeParentRequest, { textDocument: element.identifier, childId: element.node.nodeId }, lsp.CancellationToken.None);
        if (!response || !response.parent) {
            return undefined;
        }

        return { kind: 'SyntaxTreeNodeAndFile', identifier: element.identifier, node: response.parent };
    }

    private _highlightRange(range: lsp.Range) {
        const vscodeRange = new vscode.Range(
            new vscode.Position(range.start.line, range.start.character),
            new vscode.Position(range.end.line, range.end.character));

        const activeTextEditor = vscode.window.activeTextEditor;
        if (!activeTextEditor) {
            return;
        }

        if (vscode.workspace.getConfiguration("compilerDeveloperSdk").get("syncCursorWithTree")) {
            if (!this.editorChangeCausedDataChange) {
                // Only do this if the editor change didn't cause the data change. Otherwise, we'll move the cursor as the user is typing,
                // which is quite annoying.
                activeTextEditor.revealRange(vscodeRange);
            }
            else {
                this.editorChangeCausedDataChange = false;
            }
        }

        activeTextEditor.setDecorations(this._decorationType, [vscodeRange]);
    }

    private _clearHighlight() {
        const range = new vscode.Range(new vscode.Position(0, 0), new vscode.Position(0, 0));
        vscode.window.activeTextEditor?.setDecorations(this._decorationType, [range]);
    }

    dispose() {
        for (const disposable of this._disposables) {
            disposable.dispose();
        }
    }
}

enum SyntaxNodePropertyCategory {
    typeInfoHeader,
    symbolInfoHeader,
    candidateSymbolsHeader,
    declaredSymbolHeader,
    propertiesHeader,
    leafNode
}

interface SyntaxNodeProperty {
    kind: "SyntaxNodeProperty";
    category: SyntaxNodePropertyCategory;
    title: string;
    hasChildren: boolean;
    icon?: vscode.ThemeIcon;
    description?: string;
    info: SyntaxNodeInfoResponse
}

function leafNode(title: string, info: SyntaxNodeInfoResponse, description?: string, symbolKind?: string): SyntaxNodeProperty {
    return {
        kind: "SyntaxNodeProperty",
        category: SyntaxNodePropertyCategory.leafNode,
        hasChildren: false,
        title,
        description,
        icon: symbolKind ? getSymbolKindIcon(symbolKind) : undefined,
        info
    };
}

const syntaxTree = new lsp.RequestType<SyntaxTreeRequest, SyntaxTreeResponse, void>('syntaxTree', lsp.ParameterStructures.auto);

interface SyntaxTreeRequest {
    textDocument: lsp.TextDocumentIdentifier;
    parentNodeId?: number;
}

interface SyntaxTreeResponse {
    nodes: SyntaxTreeNode[];
}

const syntaxNodeParentRequest = new lsp.RequestType<SyntaxNodeParentRequest, NodeParentResponse<SyntaxTreeNode>, void>('syntaxTree/parentNode', lsp.ParameterStructures.auto);

interface SyntaxNodeParentRequest {
    textDocument: lsp.TextDocumentIdentifier;
    childId: number;
}

const syntaxNodeInfoRequest = new lsp.RequestType<SyntaxNodeInfoRequest, SyntaxNodeInfoResponse, void>('syntaxTree/info', lsp.ParameterStructures.auto);

interface SyntaxNodeInfoRequest {
    textDocument: lsp.TextDocumentIdentifier;
    node: SyntaxTreeNode;
}

interface SyntaxNodeInfoResponse {
    nodeType: SymbolAndKind;
    nodeSyntaxKind: string;
    semanticClassification?: string;
    nodeSymbolInfo?: NodeSymbolInfo;
    nodeTypeInfo?: NodeTypeInfo;
    nodeDeclaredSymbol: SymbolAndKind;
    properties: object;
}

interface NodeSymbolInfo {
    symbol: SymbolAndKind;
    candidateReason: string;
    candidateSymbols: SymbolAndKind[];
}

interface NodeTypeInfo {
    type: SymbolAndKind;
    convertedType: SymbolAndKind;
    conversion?: string;
}

const syntaxNodeAtRangeRequest = new lsp.RequestType<NodeAtRangeRequest, NodeAtRangeResponse<SyntaxTreeNode>, void>('syntaxTree/nodeAtRange', lsp.ParameterStructures.auto);

type TreeNode =
    SyntaxTreeNodeAndFile
    | PropertiesRoot
    | SyntaxNodeProperty;

interface SyntaxTreeNodeAndFile {
    kind: "SyntaxTreeNodeAndFile";
    node: SyntaxTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface PropertiesRoot {
    kind: "PropertiesRoot";
    node: SyntaxTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface SyntaxTreeNode {
    nodeType: SymbolAndKind;
    range: lsp.Range;
    hasChildren: boolean;
    nodeId: number;
}
