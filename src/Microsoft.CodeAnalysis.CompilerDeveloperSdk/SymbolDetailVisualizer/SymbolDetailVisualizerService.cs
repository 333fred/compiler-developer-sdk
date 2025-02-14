using System.Collections.Immutable;
using System.Composition;
using System.Reflection;
using System.Runtime.Serialization;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class SymbolDetailsRequest
{
    [DataMember(Name = "document"), JsonPropertyName("document")]
    public required TextDocumentIdentifier Document { get; init; }

    [DataMember(Name = "symbolId"), JsonPropertyName("symbolId")]
    public required int SymbolId { get; init; }
}

[DataContract]
sealed class SymbolDetailsResponse
{
    [DataMember(Name = "symbolName"), JsonPropertyName("symbolName")]
    public required string SymbolName { get; init; }
    [DataMember(Name = "properties"), JsonPropertyName("properties")]
    public required ImmutableDictionary<string, SymbolPropertyValue> Properties { get; init; }
}

[DataContract]
[KnownType(typeof(StringSymbolPropertyValue))]
[KnownType(typeof(SymbolIdSymbolPropertyValue))]
[KnownType(typeof(SymbolIdArraySymbolPropertyValue))]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$propertyType")]
[JsonDerivedType(typeof(StringSymbolPropertyValue), typeDiscriminator: "string")]
[JsonDerivedType(typeof(SymbolIdSymbolPropertyValue), typeDiscriminator: "symbolId")]
[JsonDerivedType(typeof(SymbolIdArraySymbolPropertyValue), typeDiscriminator: "symbolIdArray")]
abstract class SymbolPropertyValue { }

[DataContract]
sealed class StringSymbolPropertyValue : SymbolPropertyValue
{
    [DataMember(Name = "value"), JsonPropertyName("value")]
    public required string Value { get; init; }
}

[DataContract]
sealed class SymbolIdSymbolPropertyValue : SymbolPropertyValue
{
    [DataMember(Name = "value"), JsonPropertyName("value")]
    public required int Value { get; init; }
}

[DataContract]
sealed class SymbolIdArraySymbolPropertyValue : SymbolPropertyValue
{
    [DataMember(Name = "value"), JsonPropertyName("value")]
    public required ImmutableArray<int> Value { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(SyntaxNodeInfoService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.SymbolDetails)]
sealed class SymbolDetailVisualizerService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<SymbolDetailsRequest, SymbolDetailsResponse?>
{
    // Cache the reflection accessors for ISymbol and all derived interfaces
    private static readonly ImmutableArray<PropertyInfo> s_symbolProperties = [.. typeof(ISymbol).GetProperties()];

    public SymbolDetailVisualizerService() : base()
    {
    }

    public override bool MutatesSolutionState => false;
    public override bool RequiresLSPSolution => true;
    public override Uri GetTextDocumentIdentifier(SymbolDetailsRequest request) => request.Document.Uri;

    public override Task<SymbolDetailsResponse?> HandleRequestAsync(SymbolDetailsRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cache = context.GetRequiredService<SymbolDetailVisualizerDocumentCache>();
        var document = context.GetRequiredDocument();

        if (!cache.TryGetCachedEntry(document, out var documentSymbols))
        {
            return Task.FromResult<SymbolDetailsResponse?>(null);
        }

        if (!documentSymbols.TryGetSymbol(request.SymbolId, out var requestedSymbol))
        {
            return Task.FromResult<SymbolDetailsResponse?>(null);
        }

        var properties = ImmutableDictionary.CreateBuilder<string, SymbolPropertyValue>();

        // Use reflection to go find the ISymbol or derived interface properties on this instance and add them to
        // the dictionary

        foreach (var property in requestedSymbol.GetType().GetProperties())
        {
            var value = property.GetValue(requestedSymbol);
            if (value is ISymbol symbol)
            {
                properties.Add(property.Name, new SymbolIdSymbolPropertyValue { Value = documentSymbols.GetId(symbol) });
            }
            else if (value is ImmutableArray<ISymbol> symbols)
            {
                var symbolIds = symbols.Select(s => documentSymbols.GetId(s)).ToImmutableArray();
                properties.Add(property.Name, new SymbolIdArraySymbolPropertyValue { Value = symbolIds });
            }
            else if (value is string str)
            {
                properties.Add(property.Name, new StringSymbolPropertyValue { Value = str });
            }
        }

        return Task.FromResult<SymbolDetailsResponse?>(null);
    }
}
