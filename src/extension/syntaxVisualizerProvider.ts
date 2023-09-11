import * as vscode from 'vscode';
import { CSharpExtension } from "./csharpExtensionExports";
import * as lsp from 'vscode-languageserver-protocol';
import assert = require('node:assert');
import { Logger } from './logger';
import { SymbolAndKind, getSymbolKindIcon } from './common';

export function createSyntaxVisualizerProvider(csharpExtension: CSharpExtension, logger: Logger): vscode.Disposable[] {
    const syntaxTreeProvider = new SyntaxTreeProvider(csharpExtension, logger);
    const treeView = vscode.window.createTreeView('syntaxTree', { treeDataProvider: syntaxTreeProvider });
    const propertyTreeProvider = new SyntaxNodePropertyTreeProvider();
    const propertyViewDisposable = vscode.window.registerTreeDataProvider('syntaxProperties', propertyTreeProvider);

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

            await treeView.reveal({ node: response.node, identifier: textDocument });
            const responseRange = response.node.range;
            const highlightRange = new vscode.Range(
                new vscode.Position(responseRange.start.line, responseRange.start.character),
                new vscode.Position(responseRange.end.line, responseRange.end.character));
            await vscode.commands.executeCommand(highlightEditorRangeCommand, highlightRange);
        }
    });

    const treeViewVisibilityDisposable = treeView.onDidChangeVisibility(async (event) => {
        if (!event.visible) {
            propertyTreeProvider.setSyntaxNodeInfo(undefined);
            await vscode.commands.executeCommand(clearHighlightCommand);
        }
    });

    const treeViewSelectionChangedDisposable = treeView.onDidChangeSelection(async (event) => {
        if (event.selection && event.selection.length > 0) {
            const activeNode = event.selection[0];
            try {
                const info = await csharpExtension.experimental.sendServerRequest(syntaxNodeInfoRequest, { textDocument: activeNode.identifier, node: activeNode.node }, lsp.CancellationToken.None);
                propertyTreeProvider.setSyntaxNodeInfo(info);
            }
            catch (e) {
                console.log(`Error getting syntax node info: ${e}`);
                propertyTreeProvider.setSyntaxNodeInfo(undefined);
            }
        }
        else {
            propertyTreeProvider.setSyntaxNodeInfo(undefined);
        }
    });

    return [treeView, propertyViewDisposable, editorTextSelectionChangeDisposable, treeViewVisibilityDisposable, treeViewSelectionChangedDisposable];
}

const highlightEditorRangeCommand: string = 'csharp.syntaxTreeVisualizer.highlightRange';
const clearHighlightCommand: string = 'csharp.syntaxTreeVisualizer.clearHighlight';

class SyntaxTreeProvider implements vscode.TreeDataProvider<SyntaxTreeNodeAndFile>, vscode.Disposable {

    private readonly _wordHighlightBackground: vscode.ThemeColor;
    private readonly _wordHighlightBorder: vscode.ThemeColor;
    private readonly _decorationType: vscode.TextEditorDecorationType;
    private readonly _disposables: vscode.Disposable[];
    private readonly _onDidChangeTreeData: vscode.EventEmitter<SyntaxTreeNodeAndFile | undefined> = new vscode.EventEmitter<SyntaxTreeNodeAndFile | undefined>();

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
                this._onDidChangeTreeData.fire(undefined);
                
            }
        });

        const highlightRangeCommandDisposable = vscode.commands.registerCommand(highlightEditorRangeCommand, (node) => this._highlightRange(node), this);
        const clearHighlightCommandDisposable = vscode.commands.registerCommand(clearHighlightCommand, () => this._clearHighlight(), this);

        this._disposables = [activeEditorDisposable, textDocumentChangedDisposable, highlightRangeCommandDisposable, clearHighlightCommandDisposable, this._onDidChangeTreeData];
    }

    readonly onDidChangeTreeData: vscode.Event<SyntaxTreeNodeAndFile | undefined> = this._onDidChangeTreeData.event;

    getTreeItem(element: SyntaxTreeNodeAndFile): vscode.TreeItem {
        const node = element.node;
        let treeItem = new vscode.TreeItem(`${node.nodeType.symbol}`, node.hasChildren ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None);
        treeItem.description = `[${node.range.start.line}:${node.range.start.character}-${node.range.end.line}:${node.range.end.character})`;
        treeItem.command = { "title": "Highlight Range", command: highlightEditorRangeCommand, arguments: [node.range] };
        treeItem.iconPath = getSymbolKindIcon(node.nodeType.symbolKind);
        treeItem.id = `${node.nodeId}`;

        return treeItem;
    }

    async getChildren(element?: SyntaxTreeNodeAndFile): Promise<SyntaxTreeNodeAndFile[]> {
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

        if (!children || !Array.isArray(children.nodes)) {
            return [];
        }

        return children.nodes.map(node => { return { node, identifier }; });
    }

    async getParent(element: SyntaxTreeNodeAndFile): Promise<SyntaxTreeNodeAndFile | undefined> {
        const response = await this.server.experimental.sendServerRequest(syntaxNodeParentRequest, { textDocument: element.identifier, childId: element.node.nodeId }, lsp.CancellationToken.None);
        if (!response || !response.parent) {
            return undefined;
        }

        return { identifier: element.identifier, node: response.parent };
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
            activeTextEditor.revealRange(vscodeRange);
            activeTextEditor.selection = new vscode.Selection(vscodeRange.start, vscodeRange.start);
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
    category: SyntaxNodePropertyCategory;
    title: string;
    hasChildren: boolean;
    icon?: vscode.ThemeIcon;
    description?: string;
}

class SyntaxNodePropertyTreeProvider implements vscode.TreeDataProvider<SyntaxNodeProperty> {
    private _syntaxNodeInfo?: SyntaxNodeInfoResponse;
    private _onDidChangeTreeData: vscode.EventEmitter<SyntaxNodeProperty | undefined> = new vscode.EventEmitter<SyntaxNodeProperty | undefined>();
    readonly onDidChangeTreeData: vscode.Event<SyntaxNodeProperty | undefined> = this._onDidChangeTreeData.event;

    public setSyntaxNodeInfo(newInfo?: SyntaxNodeInfoResponse) {
        this._syntaxNodeInfo = newInfo;
        this._onDidChangeTreeData.fire(undefined);
    }

    getChildren(element?: SyntaxNodeProperty): SyntaxNodeProperty[] | undefined {
        if (!this._syntaxNodeInfo) {
            return undefined;
        }

        if (!element) {
            let categories: SyntaxNodeProperty[] = [
                leafNode('Node Type:', this._syntaxNodeInfo.nodeType.symbol, this._syntaxNodeInfo.nodeType.symbolKind),
                leafNode('SyntaxKind:', this._syntaxNodeInfo.nodeSyntaxKind, "EnumMember"),
            ];

            if (this._syntaxNodeInfo.semanticClassification) {
                categories.push(leafNode('Semantic Classification', this._syntaxNodeInfo.semanticClassification));
            }

            if (this._syntaxNodeInfo.nodeTypeInfo) {
                categories.push({ category: SyntaxNodePropertyCategory.typeInfoHeader, title: 'Type Info', hasChildren: true });
            }
            else {
                categories.push({ category: SyntaxNodePropertyCategory.typeInfoHeader, title: 'Type Info:', description: '<null>', hasChildren: false });
            }

            if (this._syntaxNodeInfo.nodeSymbolInfo) {
                categories.push({ category: SyntaxNodePropertyCategory.symbolInfoHeader, title: 'Symbol Info', hasChildren: true });
            }
            else {
                categories.push({ category: SyntaxNodePropertyCategory.symbolInfoHeader, title: 'Symbol Info:', description: '<null>', hasChildren: false });
            }

            categories.push({
                category: SyntaxNodePropertyCategory.declaredSymbolHeader,
                title: 'Declared Symbol:',
                description: this._syntaxNodeInfo.nodeDeclaredSymbol.symbol,
                icon: this._syntaxNodeInfo.nodeDeclaredSymbol.symbolKind
                    ? getSymbolKindIcon(this._syntaxNodeInfo.nodeDeclaredSymbol.symbolKind)
                    : undefined,
                hasChildren: false
            });

            categories.push({
                category: SyntaxNodePropertyCategory.propertiesHeader,
                title: 'Properties',
                hasChildren: Object.keys(this._syntaxNodeInfo.properties).length !== 0
            });

            return categories;
        }

        switch (element.category) {
            case SyntaxNodePropertyCategory.declaredSymbolHeader:
            case SyntaxNodePropertyCategory.leafNode:
                return undefined;

            case SyntaxNodePropertyCategory.typeInfoHeader:
                assert(this._syntaxNodeInfo.nodeTypeInfo);
                return [
                    leafNode('Type:', this._syntaxNodeInfo.nodeTypeInfo.type.symbol, this._syntaxNodeInfo.nodeTypeInfo.type.symbolKind),
                    leafNode('ConvertedType:', this._syntaxNodeInfo.nodeTypeInfo.convertedType.symbol, this._syntaxNodeInfo.nodeTypeInfo.convertedType.symbolKind),
                    leafNode('Conversion:', this._syntaxNodeInfo.nodeTypeInfo.conversion)
                ];

            case SyntaxNodePropertyCategory.symbolInfoHeader:
                assert(this._syntaxNodeInfo.nodeSymbolInfo);
                let symbolInfoNodes = [
                    leafNode('Symbol:', this._syntaxNodeInfo.nodeSymbolInfo.symbol.symbol, this._syntaxNodeInfo.nodeSymbolInfo.symbol.symbolKind),
                    leafNode('Candidate Reason:', this._syntaxNodeInfo.nodeSymbolInfo.candidateReason)
                ];

                if (this._syntaxNodeInfo.nodeSymbolInfo.candidateSymbols.length > 0) {
                    symbolInfoNodes.push({ category: SyntaxNodePropertyCategory.candidateSymbolsHeader, title: 'Candidate Symbols', hasChildren: true });
                }
                else {
                    symbolInfoNodes.push(leafNode('Candidate Symbols', 'None'));
                }

                return symbolInfoNodes;

            case SyntaxNodePropertyCategory.candidateSymbolsHeader:
                assert(this._syntaxNodeInfo.nodeSymbolInfo!.candidateSymbols.length > 0);
                return this._syntaxNodeInfo.nodeSymbolInfo!.candidateSymbols.map(s => leafNode(s.symbol, undefined, s.symbolKind));

            case SyntaxNodePropertyCategory.propertiesHeader:
                let properties: SyntaxNodeProperty[] = [];
                for (const [key, value] of Object.entries(this._syntaxNodeInfo.properties).sort((a, b) => a[0].localeCompare(b[0]))) {
                    properties.push(leafNode(key, value));
                }

                return properties;
        }
    }

    getTreeItem(property: SyntaxNodeProperty): vscode.TreeItem {
        const collapsibleState = property.hasChildren
            ? (property.category === SyntaxNodePropertyCategory.propertiesHeader ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.Expanded)
            : vscode.TreeItemCollapsibleState.None;

        let treeItem = new vscode.TreeItem(property.title, collapsibleState);
        treeItem.iconPath = property.icon;
        treeItem.description = property.description;

        return treeItem;
    }
}

function leafNode(title: string, description?: string, symbolKind?: string): SyntaxNodeProperty {
    return {
        category: SyntaxNodePropertyCategory.leafNode,
        hasChildren: false,
        title,
        description,
        icon: symbolKind ? getSymbolKindIcon(symbolKind) : undefined
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

const syntaxNodeParentRequest = new lsp.RequestType<SyntaxNodeParentRequest, SyntaxNodeParentResponse, void>('syntaxTree/parentNode', lsp.ParameterStructures.auto);

interface SyntaxNodeParentRequest {
    textDocument: lsp.TextDocumentIdentifier;
    childId: number;
}

interface SyntaxNodeParentResponse {
    parent?: SyntaxTreeNode;
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

const syntaxNodeAtRangeRequest = new lsp.RequestType<SyntaxNodeAtRangeRequest, SyntaxNodeAtRangeResponse, void>('syntaxTree/nodeAtRange', lsp.ParameterStructures.auto);

interface SyntaxNodeAtRangeRequest {
    textDocument: lsp.TextDocumentIdentifier;
    range: lsp.Range;
}

interface SyntaxNodeAtRangeResponse {
    node?: SyntaxTreeNode;
}

interface SyntaxTreeNodeAndFile {
    node: SyntaxTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface SyntaxTreeNode {
    nodeType: SymbolAndKind;
    range: lsp.Range;
    hasChildren: boolean;
    nodeId: number;
}
