// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.NetFramework.Analyzers;

namespace Microsoft.NetFramework.CSharp.Analyzers
{
    /// <summary>
    /// CA2236: Call base class methods on ISerializable types
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpCallBaseClassMethodsOnISerializableTypesFixer : CallBaseClassMethodsOnISerializableTypesFixer
    {
    }
}
