// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { CSharpExtension } from './csharpExtensionExports';
import { createSyntaxVisualizerProvider } from './syntaxVisualizerProvider';
import { log } from 'node:console';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {

    const csharpExtension = vscode.extensions.getExtension('ms-dotnettools.csharp')?.exports as CSharpExtension;

    if (!csharpExtension) {
        throw new Error('Could not find C# extension');
    }

    await csharpExtension.initializationFinished();

    const disposables = createSyntaxVisualizerProvider(csharpExtension);
    context.subscriptions.push(...disposables);
}

// This method is called when your extension is deactivated
export function deactivate() {}
