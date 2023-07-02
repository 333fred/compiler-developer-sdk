// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from 'vscode';
import { CSharpExtension } from './csharpExtensionExports';
import { createSyntaxVisualizerProvider } from './syntaxVisualizerProvider';
import path = require('node:path');
import { copyFileSync, existsSync } from 'node:fs';

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export async function activate(context: vscode.ExtensionContext) {
    const csharpExtension = vscode.extensions.getExtension('ms-dotnettools.csharp')?.exports as CSharpExtension;

    if (!csharpExtension) {
        throw new Error('Could not find C# extension');
    }

    const installStatus = await installSdk(csharpExtension);

    if (installStatus === InstallStatus.needsRestart) {
        const selection = await vscode.window.showInformationMessage('The C# Compiler Developer SDK has been installed. Restart the C# LSP to enable features.', 'Restart', 'Cancel');
        if (selection !== 'Restart') {
            // Can't enable any features, so we bail
            // TODO: Listen for a manual restart by the user and enable at that point
            return;
        }

        await vscode.commands.executeCommand('dotnet.restartServer');
    }

    await csharpExtension.initializationFinished();

    const disposables = createSyntaxVisualizerProvider(csharpExtension);
    context.subscriptions.push(...disposables);
}

async function installSdk(csharp: CSharpExtension): Promise<InstallStatus> {
    return await vscode.window.withProgress({ location: vscode.ProgressLocation.Window, title: 'Installing Compiler SDK', cancellable: false }, async (progress) => {
        const lspPath = await csharp.serverExecutablePath();

        if (!lspPath) {
            vscode.window.showErrorMessage('Could not find C# language server');
            return InstallStatus.error;
        }

        const ea = 'Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSDK.dll';
        const sdk = 'Microsoft.CodeAnalysis.CompilerDeveloperSdk.dll';
        const lspDir = path.dirname(lspPath);

        progress.report({ message: 'Checking for existing install', increment: 10 });

        if (existsSync(path.join(lspDir, ea)) && existsSync(path.join(lspDir, sdk))) {
            // TODO: Version check. For now, we just bail
            progress.report({ message: 'Existing install found. Done.', increment: 90 });
            return InstallStatus.alreadyInstalled;
        }

        progress.report({ message: 'No install found. Copying SDK to language server directory', increment: 10 });

        if (!copyFile(ea) || !copyFile(sdk)) {
            return InstallStatus.error;
        }

        return InstallStatus.needsRestart;

        function copyFile(inputFileName: string): boolean {
            const sourceFile = path.join(__dirname, inputFileName);
            const destinationFile = path.join(lspDir, inputFileName);
            copyFileSync(sourceFile, destinationFile);
            if (!existsSync(destinationFile)) {
                vscode.window.showErrorMessage(`Error copying ${inputFileName}.`);
                return false;
            }

            return true;
        }
    });
}

enum InstallStatus {
    error,
    alreadyInstalled,
    needsRestart,
}

// This method is called when your extension is deactivated
export function deactivate() { }
