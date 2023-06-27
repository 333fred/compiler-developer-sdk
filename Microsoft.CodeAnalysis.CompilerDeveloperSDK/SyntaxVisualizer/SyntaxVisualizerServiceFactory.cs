using System.Composition;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSDK;

[Shared]
[ExportCompilerDeveloperSdkLspServiceFactory(typeof(SyntaxVisualizerServiceFactory), Endpoints.SyntaxTree)]
class SyntaxVisualizerServiceFactory : ICompilerDeveloperSdkLspServiceFactory
{
    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public SyntaxVisualizerServiceFactory()
    {
    }

    public ICompilerDeveloperSdkLspService CreateILspService()
    {
        throw new NotImplementedException();
    }
}
