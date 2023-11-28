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
            await treeView.reveal({ kind: response.node.ioperationInfo ? 'ioperation' : 'symbol', node: response.node, identifier: textDocument });
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

    const collapseAllDisposable = vscode.commands.registerCommand(
        'compilerDeveloperSdk.collapseIOperationTree',
        () => vscode.commands.executeCommand('workbench.actions.treeView.operationTree.collapseAll'));

    return [treeView, editorTextSelectionChangeDisposable, treeViewVisibilityDisposable, collapseAllDisposable];
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
            let treeItem = new vscode.TreeItem(`${node.nodeType.symbol}`, vscode.TreeItemCollapsibleState.Collapsed);
            treeItem.description = `[${node.range.start.line}:${node.range.start.character}-${node.range.end.line}:${node.range.end.character})`;
            treeItem.command = { "title": "Highlight Range", command: highlightEditorRangeCommand, arguments: [node.range] };
            treeItem.iconPath = getSymbolKindIcon(node.nodeType.symbolKind);
            treeItem.id = `${treeItem.label}${treeItem.description}`;

            return treeItem;
        }
        else if (element.kind === 'operationsRootNode') {
            const node = element.parentNode;
            if (!node.hasIOperationChildren) {
                return new vscode.TreeItem("No IOperation children");
            }

            const treeItem = new vscode.TreeItem(`IOperation Nodes`, vscode.TreeItemCollapsibleState.Collapsed);
            treeItem.iconPath = getSymbolKindIcon("Code");
            return treeItem;
        }
        else if (element.kind === 'ioperationChild') {
            const node = (<IOperationChildNode>element).child;

            const treeItem = new vscode.TreeItem(`${node.name}`, node.isPresent ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None);
            treeItem.description = node.isPresent
                ? ""
                : node.isArray ? "[]" : "null";
            treeItem.iconPath = node.isArray ? getSymbolKindIcon("List") : getSymbolKindIcon("Property");

            return treeItem;
        }
        else if (element.kind === 'propertiesNode') {
            return new vscode.TreeItem(`Properties`, vscode.TreeItemCollapsibleState.Collapsed);
        }
        else {
            const node = <PropertyNode>element;
            const treeItem = new vscode.TreeItem(`${node.name}`, vscode.TreeItemCollapsibleState.None);
            treeItem.description = node.description;
            treeItem.id = `${treeItem.label}${treeItem.description}`;
            return treeItem;
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

            const nonSymbolChildren: TreeNode[] = element?.node.hasIOperationChildren
                ? [{ kind: "operationsRootNode", identifier, parentNode: element.node }]
                : [];

            if (element && element.node.properties) {
                nonSymbolChildren.push({ kind: "propertiesNode", identifier, parentNode: element.node });
            }

            return nonSymbolChildren.concat(children.nodes.map(node => { return { kind: 'symbol', node, identifier }; }));
        }
        else if (element.kind === "operationsRootNode") {
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
            const parent = element.parentNode;
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
        else if (element.kind === "property") {
            return [];
        }
        else if (element.kind === "propertiesNode") {
            const children: TreeNode[] = [];
            for (const [key, value] of Object.entries(element.parentNode.properties!).sort((a, b) => a[0].localeCompare(b[0]))) {
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
                return (<IOperationChildNode>{ kind: 'ioperationChild', child, parentNode: node.node, identifier: element.identifier });
            }));
            children.push({ kind: 'propertiesNode', parentNode: node.node, identifier: element.identifier });

            return children;
        }
    }

    async getParent(element: TreeNode): Promise<TreeNode | undefined> {
        let identifier: lsp.TextDocumentIdentifier;
        let childNode: IOperationTreeNode;
        if (element.kind === 'symbol' || element.kind === 'ioperation') {
            identifier = element.identifier;
            childNode = element.node;
        }
        else if (element.kind === 'operationsRootNode') {
            const node = <TextNode>element;
            return <IOperationTreeNodeAndFile>{ kind: 'symbol', node: node.parentNode, identifier: node.identifier };
        }
        else if (element.kind === 'ioperationChild') {
            return <IOperationTreeNodeAndFile>{ kind: 'ioperation', node: element.parentNode, identifier: element.identifier };
        }
        else {
            return undefined;
        }

        const response = await this.server.experimental.sendServerRequest(ioperationNodeParentRequest, {
            textDocument: identifier,
            childSymbolId: childNode.symbolId,
            childIOperationId: childNode.ioperationInfo?.ioperationId
        }, lsp.CancellationToken.None);
        if (!response || !response.parent) {
            return undefined;
        }

        let kind: 'symbol' | 'ioperation' = 'symbol';
        if (element.kind === 'ioperation') {
            // Reached the end of the 
            if (!response.parent.ioperationInfo) {
                // Reached the end of the ioperation nodes. Return iop parent collapsible node
                return { kind: "operationsRootNode", identifier: element.identifier, parentNode: response.parent };
            }

            return { kind: 'ioperationChild', child: element.node.ioperationInfo!.parentInfo!, parentNode: response.parent, identifier: element.identifier };
        }

        return { kind, identifier: identifier, node: response.parent };
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

const ioperationNodeParentRequest = new lsp.RequestType<IOperationNodeParentRequest, IOperationNodeParentResponse, void>('operationTree/parentNode', lsp.ParameterStructures.auto);

interface IOperationNodeParentRequest {
    textDocument: lsp.TextDocumentIdentifier;
    childSymbolId: number;
    childIOperationId?: number;
}

interface IOperationNodeParentResponse extends NodeParentResponse<IOperationTreeNode> {
    parentOperationPropertyName?: string;
    isArray: boolean;
}

const operationNodeAtRangeRequest = new lsp.RequestType<NodeAtRangeRequest, NodeAtRangeResponse<IOperationTreeNode>, void>('operationTree/nodeAtRange', lsp.ParameterStructures.auto);

type TreeNode = IOperationTreeNodeAndFile | TextNode | IOperationChildNode | PropertyNode;

interface IOperationTreeNodeAndFile {
    kind: "symbol" | "ioperation";
    node: IOperationTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface TextNode {
    kind: "operationsRootNode" | "propertiesNode";
    parentNode: IOperationTreeNode;
    identifier: lsp.TextDocumentIdentifier;
}

interface IOperationChildNode {
    kind: "ioperationChild"
    child: OperationChild;
    parentNode: IOperationTreeNode;
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
    properties?: object;
}

interface IOperationNodeInformation {
    parentInfo?: OperationChild;
    ioperationId: number;
    operationChildrenInfo: OperationChild[];
}

interface OperationChild {
    name: string;
    isArray: boolean;
    isPresent: boolean;
}
