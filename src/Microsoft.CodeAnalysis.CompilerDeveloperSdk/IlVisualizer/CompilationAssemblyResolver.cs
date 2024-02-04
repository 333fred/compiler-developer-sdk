using System.Collections.Frozen;

using ICSharpCode.Decompiler.Metadata;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

sealed class CompilationAssemblyResolver(Compilation compilation) : IAssemblyResolver
{
    private readonly Compilation _compilation = compilation;
    private readonly FrozenDictionary<string, MetadataReference> _metadataReferences = compilation.References.ToFrozenDictionary(keySelector: r => ((IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(r)!).Name);

    public PEFile? Resolve(IAssemblyReference reference)
    {
        if (_metadataReferences.TryGetValue(reference.Name, out var metadataReference))
        {
            return new PEFile(metadataReference.Display!);
        }
        return null;
    }

    public Task<PEFile?> ResolveAsync(IAssemblyReference reference)
    {
        return Task.FromResult(Resolve(reference));
    }

    public PEFile? ResolveModule(PEFile mainModule, string moduleName)
    {
        throw new NotImplementedException();
    }

    public Task<PEFile?> ResolveModuleAsync(PEFile mainModule, string moduleName)
    {
        throw new NotImplementedException();
    }
}
