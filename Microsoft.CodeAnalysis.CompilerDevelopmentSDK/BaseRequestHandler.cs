using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CommonLanguageServerProtocol.Framework;

abstract class BaseRequestHandler<TRequest, TResponse, TRequestContext> : IRequestHandler<TRequest, TResponse, TRequestContext>, IDynamicInterfaceCastable
{
    public abstract bool MutatesSolutionState { get; }

    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
    public abstract Task<TResponse> HandleRequestAsync(TRequest request, TRequestContext context, CancellationToken cancellationToken);

    RuntimeTypeHandle IDynamicInterfaceCastable.GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
    {
        Debug.Assert(interfaceType.Equals(ILspServiceHandleInformation.ILanguageServiceTypeHandle));
        return ILspServiceHandleInformation.ILanguageServiceTypeImplementationHandle;
    }

    bool IDynamicInterfaceCastable.IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented) => interfaceType.Equals(ILspServiceHandleInformation.ILanguageServiceTypeHandle);
}
