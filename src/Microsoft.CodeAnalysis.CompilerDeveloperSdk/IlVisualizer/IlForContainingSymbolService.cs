using System.Composition;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
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
    public required IlForContainingSymbol? Il { get; init; }
    [DataMember(Name = "success")]
    public required bool Success { get; init; }
    [DataMember(Name = "errors")]
    public required string? Errors { get; init; }
}

[DataContract]
sealed class IlForContainingSymbol
{
    [DataMember(Name = "fullSymbolName")]
    public required string FullSymbolName { get; init; }
    [DataMember(Name = "il")]
    public required string Il { get; init; }
    [DataMember(Name = "decompiledSource")]
    public required string DecompiledSource { get; init; }
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

    private static readonly SymbolDisplayFormat FullyQualifiedWithoutGlobal = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

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

        ISymbol? declaredSymbol = await FindContext(request, cts, document, cancellationToken);

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
                Success = false,
                Errors = "Could not find a containing context for the given position"
            };
        }

        ISymbol? typeOrAssemblyContext = declaredSymbol as INamedTypeSymbol ?? (ISymbol)declaredSymbol.ContainingType ?? (IAssemblySymbol)declaredSymbol;

        // Wait for emit to finish
        var emitResult = await compilationResultTask;
        if (!emitResult.Success)
        {
            return new()
            {
                Il = null,
                Success = false,
                Errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.ToString()))
            };
        }

        ilStream.Position = 0;
        var peFile = new PEFile("", ilStream);
        string decompiledCSharp = DecompileIl(compilation, typeOrAssemblyContext, peFile);

        string ilOutput = DisassembleIl(typeOrAssemblyContext, peFile, cancellationToken);

        return new()
        {
            Il = new()
            {
                FullSymbolName = typeOrAssemblyContext.ToDisplayString(FullyQualifiedWithoutGlobal),
                Il = ilOutput,
                DecompiledSource = decompiledCSharp
            },
            Success = true,
            Errors = null
        };
    }

    private static async Task<ISymbol?> FindContext(IlForContainingSymbolRequest request, CancellationTokenSource cts, Document document, CancellationToken cancellationToken)
    {
        var linePosition = ProtocolConversions.PositionToLinePosition(request.Position);
        var sourceText = await document.GetTextAsync(cancellationToken);
        var position = sourceText.Lines.GetPosition(linePosition);

        var tree = await document.GetSyntaxTreeAsync(cts.Token);
        var node = (await tree!.GetRootAsync(cancellationToken)).FindToken(position).Parent;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        Debug.Assert(semanticModel is not null);

        for (var currentNode = node; currentNode is not null; currentNode = currentNode.Parent)
        {
            if (semanticModel.GetDeclaredSymbol(currentNode, cancellationToken: cts.Token) is ISymbol
                {
                    Kind: SymbolKind.Method or SymbolKind.Property or SymbolKind.Field or SymbolKind.NamedType or SymbolKind.Event
                } symbol)
            {
                return symbol;
            }
        }

        // Didn't find a type, so we're in a compilation context, so return the containing assembly
        return semanticModel.Compilation.Assembly;
    }

    static string DecompileIl(Compilation compilation, ISymbol typeOrAssemblyContext, PEFile peFile)
    {
        var decompiler = new CSharpDecompiler(peFile, new CompilationAssemblyResolver(compilation), DecompilerSettings);
        // TODO: Handle assembly context
        var decompiledCSharp = decompiler.DecompileTypeAsString(new ICSharpCode.Decompiler.TypeSystem.FullTypeName(GetMetadataName(typeOrAssemblyContext)));
        return decompiledCSharp;

        static string GetMetadataName(ISymbol symbol)
        {
            var builder = new StringBuilder();
            BuildNamedType(symbol, builder);
            return builder.ToString();

            static void BuildNamedType(ISymbol symbol, StringBuilder builder)
            {
                if (symbol is INamespaceSymbol namespaceSymbol)
                {
                    builder.Append(namespaceSymbol.ToDisplayString(FullyQualifiedWithoutGlobal));
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

    private static string DisassembleIl(ISymbol containingType, PEFile peFile, CancellationToken cancellationToken)
    {
        var ilOutput = new PlainTextOutput();
        var reflectionDisassembler = new ReflectionDisassembler(ilOutput, cancellationToken);

        var metadataReader = peFile.Metadata;
        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var type = metadataReader.GetTypeDefinition(typeHandle);
            var typeName = metadataReader.GetString(type.Name);
            // TODO: Handle disassembling the whole assembly
            if (typeName == containingType.MetadataName)
            {
                reflectionDisassembler.DisassembleType(peFile, typeHandle);
                break;
            }
        }

        return ilOutput.ToString();
    }
}
