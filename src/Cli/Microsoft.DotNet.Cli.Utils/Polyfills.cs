// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Runtime.CompilerServices {

    internal static class IsExternalInit
    {
    }

}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        public bool ReturnValue { get; }
    }
}
#pragma warning restore IDE0130 // Namespace does not match folder structure

#endif
