// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { CSharpExtension } from './csharpExtensionExports';
import { createSyntaxVisualizerProvider } from './syntaxVisualizerProvider';
import { createLogger } from './logger';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {
    var output = vscode.window.createOutputChannel(".NET Compiler Developer SDK");
    var logger = createLogger(output);
    logger.log("Extension activated");

    const csharpExtension = vscode.extensions.getExtension('ms-dotnettools.csharp')?.exports as CSharpExtension;

    if (!csharpExtension) {
        logger.log("Could not find C# extension");
        throw new Error('Could not find C# extension');
    }

    logger.log("C# extension found, waiting for initialization to complete");

    await csharpExtension.initializationFinished();

    logger.log("C# extension initialization complete. Activating SyntaxVisualizer.");

    const disposables = createSyntaxVisualizerProvider(csharpExtension, logger);
    context.subscriptions.push(...disposables);
}

// This method is called when your extension is deactivated
export function deactivate() { }
