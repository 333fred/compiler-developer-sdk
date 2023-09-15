import * as vscode from 'vscode';
import { CSharpExtension } from "./csharpExtensionExports";
import * as lsp from 'vscode-languageserver-protocol';
import assert = require('node:assert');
import { Logger } from './logger';
import { NodeAtRangeRequest, NodeAtRangeResponse, NodeParentResponse, SymbolAndKind, getSymbolKindIcon } from './common';


export function createOperationVisualizerProvider(csharpExtension: CSharpExtension, logger: Logger): vscode.Disposable[] {
    const operationTreeProvider = new OperationTreeProvider(csharpExtension, logger);
    const treeView = vscode.window.createTreeView('operationTree', { treeDataProvider: operationTreeProvider });

    logger.log("IOperationVisualizer views registered");

    const editorTextSelectionChangeDisposable = vscode.window.onDidChangeTextEditorSelection(async event => {
        if (treeView.visible && event.selections.length > 0 && event.textEditor.document.languageId === "csharp") {
            const firstSelection = event.selections[0];
            const range: lsp.Range = lsp.Range.create(
                lsp.Position.create(firstSelection.start.line, firstSelection.start.character),
                lsp.Position.create(firstSelection.end.line, firstSelection.end.character));
            const textDocument = lsp.TextDocumentIdentifier.create(event.textEditor.document.fileName);
            const response = await csharpExtension.experimental.sendServerRequest(operationNodeAtRangeRequest, { textDocument, range }, lsp.CancellationToken.None);

            if (!response || !response.node) {
                return;
            }

            operationTreeProvider.editorChangeCausedDataChange = true;
            await treeView.reveal({ kind: 'symbol', node: response.node, identifier: textDocument });
            if (operationTreeProvider.highlightEnabled) {
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

    return [treeView, editorTextSelectionChangeDisposable, treeViewVisibilityDisposable, /*treeViewSelectionChangedDisposable*/];
}

const highlightEditorRangeCommand = 'csharp.operationTreeVisualizer.highlightRange';
const clearHighlightCommand = 'csharp.operationTreeVisualizer.clearHighlight';
const highlightOnClickCommand = 'compilerDeveloperSdk.highlightOnClickIOperation';

class OperationTreeProvider implements vscode.TreeDataProvider<TreeNode>, vscode.Disposable {

    private readonly _wordHighlightBackground: vscode.ThemeColor;
    private readonly _wordHighlightBorder: vscode.ThemeColor;
    private readonly _decorationType: vscode.TextEditorDecorationType;
    private readonly _disposables: vscode.Disposable[];
    private readonly _onDidChangeTreeData: vscode.EventEmitter<TreeNode | undefined> = new vscode.EventEmitter<TreeNode | undefined>();
    public editorChangeCausedDataChange: boolean = false;
    public highlightEnabled: boolean = false;

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
        if (element.kind === "symbol" || element.kind === "ioperation") {
            const node = element.node;
            let treeItem = new vscode.TreeItem(
                `${node.nodeType.symbol}`,
                node.hasSymbolChildren || node.hasIOperationChildren || element.kind == "ioperation"
                    ? vscode.TreeItemCollapsibleState.Collapsed
                    : vscode.TreeItemCollapsibleState.None);
            treeItem.description = `[${node.range.start.line}:${node.range.start.character}-${node.range.end.line}:${node.range.end.character})`;
            treeItem.command = { "title": "Highlight Range", command: highlightEditorRangeCommand, arguments: [node.range] };
            treeItem.iconPath = getSymbolKindIcon(node.nodeType.symbolKind);
            treeItem.id = `${node.symbolId}-${node.ioperationInfo?.ioperationId}`;

            return treeItem;
        }
        else if (element.kind === 'operationsNode') {
            const node = element.parentNode;
            if (!node.hasIOperationChildren) {
                return new vscode.TreeItem("No IOperation children");
            }

            const item = new vscode.TreeItem(`IOperation Nodes`, vscode.TreeItemCollapsibleState.Collapsed);
            item.iconPath = getSymbolKindIcon("Code");
            return item;
        }
        else if (element.kind === 'ioperationChild') {
            const node = (<IOperationChildNode>element).child;

            const item = new vscode.TreeItem(`${node.name}`, node.isPresent ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None);
            item.description = node.isPresent
                ? ""
                : node.isArray ? "[]" : "null";
            item.iconPath = node.isArray ? getSymbolKindIcon("List") : getSymbolKindIcon("Class");

            return item;
        }
        else if (element.kind === 'propertiesNode') {
            return new vscode.TreeItem(`Properties`, vscode.TreeItemCollapsibleState.Collapsed);
        }
        else {
            const node = <PropertyNode>element;
            const item = new vscode.TreeItem(`${node.name}`, vscode.TreeItemCollapsibleState.None);
            item.description = node.description;
            return item;
        }
    }

    async getChildren(element?: TreeNode): Promise<TreeNode[]> {
        if (!element || element.kind === "symbol") {
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
                symbolTree,
                { textDocument: identifier, parentSymbolId: element?.node.symbolId },
                lsp.CancellationToken.None);

            if (!children || !Array.isArray(children.nodes)) {
                return [];
            }

            var operationsNode: TreeNode[] = element?.node.hasIOperationChildren
                ? [{ kind: "operationsNode", identifier, parentNode: element.node }]
                : [];

            return operationsNode.concat(children.nodes.map(node => { return { kind: 'symbol', node, identifier }; }));
        }
        else if (element.kind === "operationsNode") {
            const operationsRoot = await this.server.experimental.sendServerRequest(
                operationChildren,
                { textDocument: element.identifier, parentSymbolId: element.parentNode.symbolId },
                lsp.CancellationToken.None);

            if (!operationsRoot || !Array.isArray(operationsRoot.nodes)) {
                return [];
            }

            return operationsRoot.nodes.map(node => { return { kind: 'ioperation', node, identifier: element.identifier }; });
        }
        else if (element.kind === "ioperationChild") {
            const parent = element.parent;
            const operationsRoot = await this.server.experimental.sendServerRequest(
                operationChildren,
                {
                    textDocument: element.identifier,
                    parentSymbolId: parent.symbolId,
                    parentIOperationId: parent.ioperationInfo!.ioperationId,
                    parentIOperationPropertyName: element.child.name
                },
                lsp.CancellationToken.None);

            if (!operationsRoot || !Array.isArray(operationsRoot.nodes)) {
                return [];
            }

            return operationsRoot.nodes.map(node => { return { kind: 'ioperation', node, identifier: element.identifier }; });
        }
        else if (element.kind == "property") {
            return [];
        }
        else if (element.kind === "propertiesNode") {
            const children: TreeNode[] = [];
            const operationInfo = element.parentNode.ioperationInfo!;
            for (const [key, value] of Object.entries(operationInfo.Properties).sort((a, b) => a[0].localeCompare(b[0]))) {
                children.push({ kind: 'property', name: key, description: value });
            }

            return children;
        }
        else {
            const children: TreeNode[] = [];

            const node = <IOperationTreeNodeAndFile>element;
            const operationInfo = node.node.ioperationInfo;
            assert(operationInfo);
            children.push(...operationInfo.operationChildrenInfo.map(child => {
                return (<IOperationChildNode>{ kind: 'ioperationChild', child, parent: node.node, identifier: element.identifier });
            }));
            children.push({ kind: 'propertiesNode', parentNode: node.node, identifier: element.identifier })

            return children;
        }
    }

    async getParent(element: IOperationTreeNodeAndFile): Promise<IOperationTreeNodeAndFile | undefined> {
        const response = await this.server.experimental.sendServerRequest(ioperationNodeParentRequest, {
            textDocument: element.identifier,
            childSymbolId: element.node.symbolId,
            childIOperationId: element.node.ioperationInfo?.ioperationId
        }, lsp.CancellationToken.None);
        if (!response || !response.parent) {
            return undefined;
        }

        // TODO: Support getting parent of IOperation node
        return { kind: 'symbol', identifier: element.identifier, node: response.parent };
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

const symbolTree = new lsp.RequestType<SymbolTreeRequest, IOperationTreeResponse, void>('operationTree', lsp.ParameterStructures.auto);

interface SymbolTreeRequest {
    textDocument: lsp.TextDocumentIdentifier;
    parentSymbolId?: number;
}

interface IOperationTreeResponse {
    nodes: IOperationTreeNode[];
}

const operationChildren = new lsp.RequestType<IOperationChildrenRequest, IOperationTreeResponse, void>('operationTree/operationChildren', lsp.ParameterStructures.auto);

interface IOperationChildrenRequest {
    textDocument: lsp.TextDocumentIdentifier;
    parentSymbolId: number;
    parentIOperationId?: number;
    parentIOperationPropertyName?: string;
}

const ioperationNodeParentRequest = new lsp.RequestType<IOperationNodeParentRequest, NodeParentResponse<IOperationTreeNode>, void>('operationTree/parentNode', lsp.ParameterStructures.auto);

interface IOperationNodeParentRequest {
    textDocument: lsp.TextDocumentIdentifier;
    childSymbolId: number;
    childIOperationId?: number;
}

const operationNodeAtRangeRequest = new lsp.RequestType<NodeAtRangeRequest, NodeAtRangeResponse<IOperationTreeNode>, void>('operationTree/nodeAtRange', lsp.ParameterStructures.auto);

type TreeNode = IOperationTreeNodeAndFile | OperationsNode | IOperationChildNode | PropertyNode;

interface IOperationTreeNodeAndFile {
    kind: "symbol" | "ioperation";
    node: IOperationTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface OperationsNode {
    kind: "operationsNode" | "propertiesNode";
    parentNode: IOperationTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface IOperationChildNode {
    kind: "ioperationChild"
    child: OperationChild;
    parent: IOperationTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface PropertyNode {
    kind: "property";
    name: string;
    description: string;
}

interface IOperationTreeNode {
    nodeType: SymbolAndKind;
    range: lsp.Range;
    hasSymbolChildren: boolean;
    hasIOperationChildren: boolean;
    symbolId: number;
    ioperationInfo?: IOperationNodeInformation;
}

interface IOperationNodeInformation {
    ioperationId: number;
    operationChildrenInfo: OperationChild[];
    Properties: object;
}

interface OperationChild {
    name: string;
    isArray: boolean;
    isPresent: boolean;
}
