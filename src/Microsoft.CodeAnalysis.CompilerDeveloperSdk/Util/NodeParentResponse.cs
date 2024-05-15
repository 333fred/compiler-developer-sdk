using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
class NodeParentResponse<T>
{
    [DataMember(Name = "parent"), JsonPropertyName("parent")]
    public T? Parent { get; init; }
}
