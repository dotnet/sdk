// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.ApiReview.CSharpAvoidCallingProblematicMethodsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiReview.CSharpAvoidCallingProblematicMethodsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiReview.BasicAvoidCallingProblematicMethodsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiReview.BasicAvoidCallingProblematicMethodsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiReview.UnitTests
{
    public class AvoidCallingProblematicMethodsTests
    {
    }
}