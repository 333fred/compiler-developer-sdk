using System.Collections.Immutable;
using System.Composition;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.CompilerDeveloperSDK;
using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.CompilerDevelopmentSDK;

[DataContract]
record SyntaxTreeRequest : ITextDocumentParams
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
}

record SyntaxTreeResponse
{
    [DataMember(Name = "syntaxTree")]
    public required ImmutableArray<SyntaxTreeNode> SyntaxTree { get; init; }
}

class SyntaxTreeService(SyntaxVisualizerCache visualizerCache) : ISyntaxVisualizerService<SyntaxTreeService>, ICompilerDeveloperSdkLspServiceDocumentRequestHandler<SyntaxTreeRequest, SyntaxTreeResponse>
{
    public bool MutatesSolutionState => false;

    public static SyntaxTreeService Create(SyntaxVisualizerCache cache) => new(cache);

    public TextDocumentIdentifier GetTextDocumentIdentifier(SyntaxTreeRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<SyntaxTreeResponse> HandleRequestAsync(SyntaxTreeRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

[Shared]
[ExportCompilerDeveloperSdkLspServiceFactory(typeof(SyntaxTreeService), Endpoints.SyntaxTree)]
class SyntaxTreeServiceFactory : BaseSyntaxVisualizerServiceFactory<SyntaxTreeService>
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxTreeServiceFactory()
    {
    }
}
