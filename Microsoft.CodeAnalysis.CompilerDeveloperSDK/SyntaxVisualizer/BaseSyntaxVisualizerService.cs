using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSDK;

abstract class BaseSyntaxVisualizerServiceFactory<TSyntaxVisualizerService> : ICompilerDeveloperSdkLspServiceFactory
    where TSyntaxVisualizerService : ISyntaxVisualizerService<TSyntaxVisualizerService>
{
    public ICompilerDeveloperSdkLspService CreateILspService(CompilerDeveloperSdkLspServices lspServices)
    {
        var cache = lspServices.GetRequiredService<SyntaxVisualizerCache>();
        return TSyntaxVisualizerService.Create(cache);
    }
}

interface ISyntaxVisualizerService<TSelf> : ICompilerDeveloperSdkLspService where TSelf : ISyntaxVisualizerService<TSelf>
{
    static abstract TSelf Create(SyntaxVisualizerCache cache);
}
