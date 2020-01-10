// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetCore.CSharp.Analyzers.InteropServices;
using Microsoft.NetCore.VisualBasic.Analyzers.InteropServices;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.InteropServices.CSharpAlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.InteropServices.BasicAlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class AlwaysConsumeTheValueReturnedByMethodsMarkedWithPreserveSigAttributeTests
    {
    }
}