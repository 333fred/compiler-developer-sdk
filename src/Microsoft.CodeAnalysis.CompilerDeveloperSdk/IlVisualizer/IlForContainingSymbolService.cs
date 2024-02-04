using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;

using Microsoft.CodeAnalysis.ExternalAccess.CompilerDeveloperSdk;

namespace Microsoft.CodeAnalysis.CompilerDeveloperSdk;

[DataContract]
sealed class IlForContainingSymbolRequest
{
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; init; }
    [DataMember(Name = "position")]
    public required Position Position { get; init; }
}

[DataContract]
sealed class IlForContainingSymbolResponse
{
    [DataMember(Name = "il")]
    public required string? Il { get; init; }
    [DataMember(Name = "decompiledSource")]
    public required string? DecompiledSource { get; init; }
    [DataMember(Name = "success")]
    public required bool Success { get; init; }
    [DataMember(Name = "errors")]
    public required string? Errors { get; init; }
}

[ExportCompilerDeveloperSdkStatelessLspService(typeof(IlForContainingSymbolService)), Shared]
[CompilerDeveloperSdkMethod(Endpoints.IlForContainingSymbol)]
sealed class IlForContainingSymbolService : AbstractCompilerDeveloperSdkLspServiceDocumentRequestHandler<IlForContainingSymbolRequest, IlForContainingSymbolResponse>
{
    private static readonly DecompilerSettings DecompilerSettings = new(LanguageVersion.CSharp1)
    {
        ArrayInitializers = false,
        AutomaticEvents = false,
        DecimalConstants = false,
        FixedBuffers = false,
        UsingStatement = false,
        SwitchStatementOnString = false,
        LockStatement = false,
        ForStatement = false,
        ForEachStatement = false,
        SparseIntegerSwitch = false,
        DoWhileStatement = false,
        StringConcat = false,
        UseRefLocalsForAccurateOrderOfEvaluation = true,
        InitAccessors = true,
        FunctionPointers = true,
        NativeIntegers = true
    };

    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public IlForContainingSymbolService()
    {
    }

    public override bool RequiresLSPSolution => true;
    public override bool MutatesSolutionState => false;

    public override Uri GetTextDocumentIdentifier(IlForContainingSymbolRequest request) => request.TextDocument.Uri;

    public override async Task<IlForContainingSymbolResponse> HandleRequestAsync(IlForContainingSymbolRequest request, RequestContext context, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var document = context.GetRequiredDocument();
        var ilStream = new MemoryStream();
        var pdbStream = new MemoryStream();
        var compilation = await document.Project.GetCompilationAsync(cts.Token);
        Debug.Assert(compilation is not null);

        // Start up emit as we find what method to decompile so we're not waiting as long on it later.
        var compilationResultTask = Task.Run(() => compilation.Emit(ilStream, pdbStream, cancellationToken: cts.Token));

        var linePosition = ProtocolConversions.PositionToLinePosition(request.Position);
        var sourceText = await document.GetTextAsync(cancellationToken);
        var position = sourceText.Lines.GetPosition(linePosition);

        var tree = await document.GetSyntaxTreeAsync(cts.Token);
        var node = (await tree!.GetRootAsync(cancellationToken)).FindToken(position).Parent;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        Debug.Assert(semanticModel is not null);

        ISymbol? declaredSymbol = null;

        for (; node is not null; node = node.Parent)
        {
            if (semanticModel.GetDeclaredSymbol(node, cancellationToken: cts.Token) is ISymbol
                {
                    Kind: SymbolKind.Method or SymbolKind.Property or SymbolKind.Field or SymbolKind.NamedType or SymbolKind.Event
                } symbol)
            {
                declaredSymbol = symbol;
                break;
            }
        }

        if (declaredSymbol is null)
        {
            // Could not find a containing context
            try
            {
                cts.Cancel();
                await compilationResultTask;
            }
            catch (OperationCanceledException) { }

            return new()
            {
                Il = null,
                DecompiledSource = null,
                Success = false,
                Errors = "Could not find a containing context for the given position"
            };
        }

        // Wait for emit to finish
        var emitResult = await compilationResultTask;
        if (!emitResult.Success)
        {
            return new()
            {
                Il = null,
                DecompiledSource = null,
                Success = false,
                Errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.ToString()))
            };
        }

        ilStream.Position = 0;
        var peFile = new PEFile("", ilStream);
        var decompiler = new CSharpDecompiler(peFile, new CompilationAssemblyResolver(compilation), DecompilerSettings);
        var decompiledCSharp = decompiler.DecompileTypeAsString(new ICSharpCode.Decompiler.TypeSystem.FullTypeName(GetMetadataName(declaredSymbol)));

        return new()
        {
            Il = null,
            DecompiledSource = decompiledCSharp,
            Success = true,
            Errors = null
        };

        static string GetMetadataName(ISymbol symbol)
        {
            if (symbol is not INamedTypeSymbol namedType)
            {
                return GetMetadataName(symbol.ContainingType);
            }

            var builder = new StringBuilder();
            BuildNamedType(namedType, builder);
            return builder.ToString();

            static void BuildNamedType(ISymbol symbol, StringBuilder builder)
            {
                if (symbol is INamespaceSymbol namespaceSymbol)
                {
                    builder.Append(namespaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)));
                    return;
                }

                Debug.Assert(symbol is INamedTypeSymbol);
                BuildNamedType(symbol.ContainingSymbol, builder);

                switch (symbol.ContainingSymbol)
                {
                    case INamespaceSymbol:
                        builder.Append('.');
                        break;
                    case INamedTypeSymbol:
                        builder.Append('+');
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                builder.Append(symbol.MetadataName);
            }
        }
    }
}
