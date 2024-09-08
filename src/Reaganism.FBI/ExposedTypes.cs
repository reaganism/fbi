// See project(System.Private.CoreLib)'s ExposedTypes.cs file for an explanation
// of the purpose of this file.

using System.Runtime.CompilerServices;

[assembly: IgnoresAccessChecksTo("System.Private.CoreLib")]

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal sealed class IgnoresAccessChecksToAttribute : Attribute
{
    public IgnoresAccessChecksToAttribute(string assemblyName)
    {
        _ = assemblyName;
    }
}