using System.Runtime.Serialization;


using LSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class NodeAtRangeRequest
{
    [DataMember(Name = "textDocument"), JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "range"), JsonPropertyName("range")]
    public required LSP.Range Range { get; init; }
}

[DataContract]
sealed class NodeAtRangeResponse<T>
{
    [DataMember(Name = "node"), JsonPropertyName("node")]
    public required T? Node { get; init; }
}
