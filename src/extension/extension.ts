// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { CSharpExtension } from './csharpExtensionExports';
import { createSyntaxVisualizerProvider } from './syntaxVisualizerProvider';
import { createLogger } from './logger';
import { createOperationVisualizerProvider } from './operationVisualizerProvider';
import { createIlVisualizer } from './ilVisualizer';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {
    const output = vscode.window.createOutputChannel(".NET Compiler Developer SDK");
    const logger = createLogger(output);
    logger.log("Extension activated");

    const csharpExtension = vscode.extensions.getExtension('ms-dotnettools.csharp')?.exports as CSharpExtension;

    if (!csharpExtension) {
        logger.log("Could not find C# extension");
        throw new Error('Could not find C# extension');
    }

    logger.log("C# extension found, waiting for initialization to complete");

    await vscode.window.withProgress({ location: vscode.ProgressLocation.Window, title: "Initializing .NET Compiler Developer SDK" }, () => {
        return csharpExtension.initializationFinished();
    });

    logger.log("C# extension initialization complete. Activating SyntaxVisualizer.");

    context.subscriptions.push(...[
        ...createSyntaxVisualizerProvider(csharpExtension, logger),
        ...createOperationVisualizerProvider(csharpExtension, logger),
        ...createIlVisualizer(csharpExtension, logger),]);

    vscode.commands.executeCommand('setContext', 'compilerDeveloperSdk.loaded', true);
}

// This method is called when your extension is deactivated
export function deactivate() { }
