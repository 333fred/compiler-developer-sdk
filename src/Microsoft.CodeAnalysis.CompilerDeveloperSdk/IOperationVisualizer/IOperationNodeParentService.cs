using System.Runtime.Serialization;

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IOperationNodeParentRequest
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "childSymbolId")]
    public required int ChildSymbolId { get; init; }
    [DataMember(Name = "childIOperationId")]
    public required int? ChildIOperationId { get; init; }
}

[DataContract]
sealed class IOperationNodeParentResponse
{
    [DataMember(Name = "parent")]
    public IOperationTreeNode? Parent { get; init; }
}
