// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpImplementISerializableCorrectlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpImplementISerializableCorrectlyFixer>;
using VerifyVB = Microsoft.CodeAnalysis.VisualBasic.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicImplementISerializableCorrectlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicImplementISerializableCorrectlyFixer>;
using Test.Utilities;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class ImplementISerializableCorrectlyTests
    {
    }
}