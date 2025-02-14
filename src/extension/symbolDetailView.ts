import * as vscode from 'vscode';
import { CSharpExtension } from "./csharpExtensionExports";
import { Logger } from './logger';

export function createSymbolDetailView(csharpExtension: CSharpExtension, logger: Logger): vscode.Disposable[] {
    const symbolDetailViewProvider = new SymbolDetailViewProvider(csharpExtension, logger);
    const treeView = vscode.window.createTreeView('symbolDetailView', { treeDataProvider: symbolDetailViewProvider });

    logger.log("SymbolDetailView views registered");

    return [];
}

class SymbolDetailViewProvider implements vscode.TreeDataProvider<TreeNode> {

    constructor(private server: CSharpExtension, private logger: Logger) {
    }

    getTreeItem(element: TreeNode): vscode.TreeItem | Thenable<vscode.TreeItem> {
        throw new Error('Method not implemented.');
    }
    getChildren(element?: any): vscode.ProviderResult<any[]> {
        throw new Error('Method not implemented.');
    }
}

interface TreeNode {

}
