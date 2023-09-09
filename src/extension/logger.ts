import * as vscode from "vscode";

export interface Logger {
    log(message: string): void;
    logDebug(message: string): void;
}

export function createLogger(output: vscode.OutputChannel): Logger {
    return new LoggerImpl(output);
}

class LoggerImpl implements Logger {
    constructor(private readonly output: vscode.OutputChannel) {
    }

    dateString(): string {
        const now = new Date();
        return `${now.getHours()}:${now.getMinutes()}:${now.getSeconds()}.${now.getMilliseconds()}`
    }

    log(message: string) {
        this.output.appendLine(`[${this.dateString()}] ${message}`);
    }

    logDebug(message: string) {
        const config = vscode.workspace.getConfiguration('compilerDeveloperSdk');
        if (config.get('verboseLogging')) {
            this.output.appendLine(`[${this.dateString()}] DEBUG ${message}`);
        }
    }
}
