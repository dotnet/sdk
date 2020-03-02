// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpProvideDeserializationMethodsForOptionalFieldsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpProvideDeserializationMethodsForOptionalFieldsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicProvideDeserializationMethodsForOptionalFieldsAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicProvideDeserializationMethodsForOptionalFieldsFixer>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class ProvideDeserializationMethodsForOptionalFieldsTests
    {
    }
}