import * as vscode from 'vscode';
import * as lsp from 'vscode-languageserver-protocol';

export interface SymbolAndKind {
    symbol: string;
    symbolKind: string;
}

export interface NodeAtRangeRequest {
    textDocument: lsp.TextDocumentIdentifier;
    range: lsp.Range;
}

export interface NodeAtRangeResponse<T> {
    node?: T;
}

export interface NodeParentResponse<T> {
    parent?: T;
}

const classIcon = new vscode.ThemeIcon('symbol-class');
const structIcon = new vscode.ThemeIcon('symbol-struct');
const enumMemberIcon = new vscode.ThemeIcon('symbol-enum-member');
const unknownIcon = new vscode.ThemeIcon('symbol-misc');
const moduleIcon = new vscode.ThemeIcon('symbol-module');
const variableIcon = new vscode.ThemeIcon('symbol-variable');
const keyIcon = new vscode.ThemeIcon('symbol-key');

const symbolKindToIcon: Map<string, vscode.ThemeIcon> = new Map();
symbolKindToIcon.set("Assembly", moduleIcon);
symbolKindToIcon.set("Array", new vscode.ThemeIcon('symbol-array'));
symbolKindToIcon.set("Class", classIcon);
symbolKindToIcon.set("Constructor", new vscode.ThemeIcon('symbol-constructor'));
symbolKindToIcon.set("Delegate", classIcon);
symbolKindToIcon.set("Discard", keyIcon);
symbolKindToIcon.set("Dynamic", classIcon);
symbolKindToIcon.set("Enum", new vscode.ThemeIcon('symbol-enum'));
symbolKindToIcon.set("EnumMember", enumMemberIcon);
symbolKindToIcon.set("Error", unknownIcon);
symbolKindToIcon.set("Event", new vscode.ThemeIcon('symbol-event'));
symbolKindToIcon.set("Field", new vscode.ThemeIcon('symbol-field'));
symbolKindToIcon.set("FunctionPointer", structIcon);
symbolKindToIcon.set("Interface", new vscode.ThemeIcon('symbol-interface'));
symbolKindToIcon.set("Label", keyIcon);
symbolKindToIcon.set("Local", variableIcon);
symbolKindToIcon.set("Method", new vscode.ThemeIcon('symbol-method'));
symbolKindToIcon.set("Module", moduleIcon);
symbolKindToIcon.set("Namespace", new vscode.ThemeIcon('symbol-namespace'));
symbolKindToIcon.set("None", variableIcon);
symbolKindToIcon.set("Operator", new vscode.ThemeIcon('symbol-operator'));
symbolKindToIcon.set("Parameter", new vscode.ThemeIcon('symbol-parameter'));
symbolKindToIcon.set("Preprocessing", keyIcon);
symbolKindToIcon.set("Property", new vscode.ThemeIcon('symbol-property'));
symbolKindToIcon.set("Pointer", structIcon);
symbolKindToIcon.set("RangeVariable", variableIcon);
symbolKindToIcon.set("Struct", structIcon);
symbolKindToIcon.set("Submission", classIcon);
symbolKindToIcon.set("TypeParameter", new vscode.ThemeIcon('symbol-type-parameter'));
symbolKindToIcon.set("Unknown", unknownIcon);

export function getSymbolKindIcon(symbolKind: string): vscode.ThemeIcon {
    return symbolKindToIcon.get(symbolKind) ?? unknownIcon;
}
