import * as vscode from 'vscode';
import { RequestType } from 'vscode-languageclient/node';

export interface CSharpExtension {
    initializationFinished: () => Promise<void>;
    sendRequest: <Params, Response, Error>(type: RequestType<Params, Response, Error>, params: Params, token: vscode.CancellationToken) => Promise<Response>;
}
