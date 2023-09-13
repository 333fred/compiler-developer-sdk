using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class NodeParentResponse<T>
{
    [DataMember(Name = "parent")]
    public T? Parent { get; init; }
}
