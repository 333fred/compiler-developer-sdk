using System.Runtime.CompilerServices;

//[assembly: IgnoresAccessChecksTo("Microsoft.CodeAnalysis.LanguageServer")]
//[assembly: IgnoresAccessChecksTo("Microsoft.CodeAnalysis.LanguageServer.Protocol")]

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
        }
    }
}