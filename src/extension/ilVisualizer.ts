import * as vscode from 'vscode';
import { Logger } from './logger';
import * as lsp from 'vscode-languageserver-protocol';
import { CSharpExtension } from './csharpExtensionExports';

const commandId = "compilerDeveloperSdk.decompileContainingContext";


export function onActivate(context: vscode.ExtensionContext, csharpExtension: CSharpExtension, logger: Logger) {
    logger.log("IL Visualizer activated");
    const ilContentProvider = new IlVisualizerDocumentProvider();
    context.subscriptions.push(vscode.workspace.registerTextDocumentContentProvider(scheme, ilContentProvider));
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

        try {

            const response = await csharpExtension.experimental.sendServerRequest(ilForContainingTypeRequest, { textDocument, position }, lsp.CancellationToken.None);
            logger.log("Decompilation complete");
            if (response.success) {
                ilContentProvider.updateContext(response.il!);
                vscode.window.showTextDocument(vscode.Uri.parse(`${scheme}:${response.il!.fullSymbolName}.cs`), { viewColumn: vscode.ViewColumn.Beside, preview: false, preserveFocus: true });
                vscode.window.showTextDocument(vscode.Uri.parse(`${scheme}:${response.il!.fullSymbolName}.il`), { viewColumn: vscode.ViewColumn.Beside, preview: false, preserveFocus: true });
            }
            else {
                logger.log(`Decompilation failed:
${response.errors}`);
                vscode.window.showErrorMessage(`Decompilation failed. See output for details.`);

            }
        }
        catch (e) {
            logger.log(`Decompilation failed: ${e}`);
            vscode.window.showErrorMessage(`Decompilation failed. See output for details.`);
        }
    }
}

const scheme = 'il-visualizer';

class IlVisualizerDocumentProvider implements vscode.TextDocumentContentProvider {
    private readonly _onDidChange = new vscode.EventEmitter<vscode.Uri>();
    private readonly _nameToContentMap = new Map<string, string>();

    public provideTextDocumentContent(uri: vscode.Uri): string {
        return this._nameToContentMap.get(uri.path) ?? "";
    }

    get onDidChange(): vscode.Event<vscode.Uri> {
        return this._onDidChange.event;
    }

    public updateContext(ilForContainingSymbol: IlForContainingSymbol): void {
        const updateUri = (ending: string, source: string) => {
            const uri = vscode.Uri.parse(`${scheme}:${ilForContainingSymbol.fullSymbolName}.${ending}`);
            this._nameToContentMap.set(uri.path, source);
            this._onDidChange.fire(uri);
        };

        updateUri("cs", ilForContainingSymbol.decompiledSource);
        updateUri("il", ilForContainingSymbol.il);
    }
}

interface IlForContainingSymbolRequest {
    textDocument: lsp.TextDocumentIdentifier;
    position: lsp.Position;
}

type IlForContainingSymbolResponse = IlForContainingSymbolResponseSuccess | IlForContainingSymbolResponseError;

interface IlForContainingSymbolResponseSuccess {
    il: IlForContainingSymbol;
    success: true;
}

interface IlForContainingSymbolResponseError {
    success: false;
    errors: string;
}

interface IlForContainingSymbol {
    fullSymbolName: string;
    il: string;
    decompiledSource: string;
}

const ilForContainingTypeRequest = new lsp.RequestType<IlForContainingSymbolRequest, IlForContainingSymbolResponse, void>('il/containingSymbol', lsp.ParameterStructures.auto);
