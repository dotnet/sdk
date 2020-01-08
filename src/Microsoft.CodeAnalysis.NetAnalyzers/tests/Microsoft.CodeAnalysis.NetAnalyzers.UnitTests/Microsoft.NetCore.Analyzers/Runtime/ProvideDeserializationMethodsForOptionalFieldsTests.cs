// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
using Test.Utilities;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpProvideDeserializationMethodsForOptionalFieldsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpProvideDeserializationMethodsForOptionalFieldsFixer>;
using VerifyVB = Microsoft.CodeAnalysis.VisualBasic.Testing.XUnit.CodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicProvideDeserializationMethodsForOptionalFieldsAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicProvideDeserializationMethodsForOptionalFieldsFixer>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class ProvideDeserializationMethodsForOptionalFieldsTests
    {
    }
}