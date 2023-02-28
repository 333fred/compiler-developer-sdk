using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

static class ILspServiceHandleInformation
{
    public static readonly Type ILanguageServiceType;
    public static readonly RuntimeTypeHandle ILanguageServiceTypeHandle;
    public static readonly RuntimeTypeHandle ILanguageServiceTypeImplementationHandle;

    static ILspServiceHandleInformation()
    {
        var lspAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Microsoft.CodeAnalysis.LanguageServer.Protocoldll");
        if (lspAssembly == null)
        {
            throw new Exception("Could not find Microsoft.CodeAnalysis.LanguageServer.Protocol.dll");
        }

        ILanguageServiceType = lspAssembly.GetType("Microsoft.CodeAnalysis.Host.ILanguageService")!;
        if (ILanguageServiceType == null)
        {
            throw new Exception("Could not find Microsoft.CodeAnalysis.Host.ILanguageService");
        }

        ILanguageServiceTypeHandle = ILanguageServiceType.TypeHandle;

        // Create a new type that implements ILanguageService and is attributed with DynamicInterfaceCastableImplementationAttribute

        var assemblyName = new AssemblyName("ILanguageServiceImplementation");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
        var typeBuilder = moduleBuilder.DefineType("ILanguageServiceImplementation", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Interface, null, new[] { ILanguageServiceType });

        var dynamicInterfaceCastableImplementationAttribute = typeof(DynamicInterfaceCastableImplementationAttribute);
        var dynamicInterfaceCastableImplementationAttributeConstructor = dynamicInterfaceCastableImplementationAttribute.GetConstructor(new[] { typeof(Type) });
        var dynamicInterfaceCastableImplementationAttributeBuilder = new CustomAttributeBuilder(dynamicInterfaceCastableImplementationAttributeConstructor!, new object[] { typeBuilder });
        typeBuilder.SetCustomAttribute(dynamicInterfaceCastableImplementationAttributeBuilder);

        var type = typeBuilder.CreateType();
        ILanguageServiceTypeImplementationHandle = type.TypeHandle;
    }
}
