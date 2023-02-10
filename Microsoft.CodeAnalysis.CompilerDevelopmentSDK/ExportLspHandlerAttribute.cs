using System.Composition;

namespace Microsoft.CodeAnalysis.CompilerDevelopmentSDK;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
class ExportLspHandlerAttribute : ExportAttribute
{

}
