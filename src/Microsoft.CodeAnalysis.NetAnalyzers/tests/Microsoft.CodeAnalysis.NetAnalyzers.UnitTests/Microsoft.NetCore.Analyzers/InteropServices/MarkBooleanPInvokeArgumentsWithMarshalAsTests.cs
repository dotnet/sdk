// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.InteropServices.CSharpMarkBooleanPInvokeArgumentsWithMarshalAsAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.InteropServices.CSharpMarkBooleanPInvokeArgumentsWithMarshalAsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.InteropServices.BasicMarkBooleanPInvokeArgumentsWithMarshalAsAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.InteropServices.BasicMarkBooleanPInvokeArgumentsWithMarshalAsFixer>;

namespace Microsoft.NetCore.Analyzers.InteropServices.UnitTests
{
    public class MarkBooleanPInvokeArgumentsWithMarshalAsTests
    {
    }
}