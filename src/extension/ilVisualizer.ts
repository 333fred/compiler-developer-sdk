import * as vscode from 'vscode';
import { Logger } from './logger';
import * as lsp from 'vscode-languageserver-protocol';
import { CSharpExtension } from './csharpExtensionExports';

const commandId = "compilerDeveloperSdk.decompileContainingContext";


export function onActivate(context: vscode.ExtensionContext, csharpExtension: CSharpExtension, logger: Logger) {
    logger.log("IL Visualizer activated");
    context.subscriptions.push(vscode.commands.registerCommand(commandId, decompileIl));

    async function decompileIl(): Promise<void> {
        logger.log("Decompiling IL");

        const activeEditor = vscode.window.activeTextEditor;
        if (!activeEditor) {
            logger.log("No active editor");
            return;
        }

        const document = activeEditor.document;
        const vscodePosition = activeEditor.selection.active;
        const textDocument = lsp.TextDocumentIdentifier.create(document.fileName);
        const position = lsp.Position.create(vscodePosition.line, vscodePosition.character);

        const response = await csharpExtension.experimental.sendServerRequest(ilForContainingTypeRequest, { textDocument, position }, lsp.CancellationToken.None);
        logger.log("Decompilation complete");
        logger.log(response.decompiledSource ?? "No decompiled source");
    }
}

interface IlForContainingSymbolRequest {
    textDocument: lsp.TextDocumentIdentifier;
    position: lsp.Position;
}

interface IlForContainingSymbolResponse {
    il: string | null;
    decompiledSource: string | null;
    success: boolean;
    errors: string | null;
}

const ilForContainingTypeRequest = new lsp.RequestType<IlForContainingSymbolRequest, IlForContainingSymbolResponse, void>('il/containingSymbol', lsp.ParameterStructures.auto);
