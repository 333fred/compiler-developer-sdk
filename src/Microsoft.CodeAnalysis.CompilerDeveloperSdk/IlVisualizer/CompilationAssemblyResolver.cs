using System.Collections.Frozen;

using ICSharpCode.Decompiler.Metadata;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

sealed class CompilationAssemblyResolver(Compilation compilation) : IAssemblyResolver
{
    private readonly FrozenDictionary<string, MetadataReference> _metadataReferences = compilation.References.ToFrozenDictionary(keySelector: r => ((IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(r)!).Name);

    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        return _metadataReferences.TryGetValue(reference.Name, out var metadataReference)
            ? File.Exists(metadataReference.Display) ? new PEFile(metadataReference.Display) : null
            : null;
    }

    public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference)
    {
        return Task.FromResult(Resolve(reference));
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName)
    {
        throw new NotSupportedException();
    }

    public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName)
    {
        throw new NotSupportedException();
    }
}
