// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetFramework.CSharp.Analyzers;
using Microsoft.NetFramework.VisualBasic.Analyzers;
using Microsoft.CodeAnalysis.Diagnostics;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetFramework.CSharp.Analyzers.CSharpDoNotMarkServicedComponentsWithWebMethodAnalyzer,
    Microsoft.NetFramework.CSharp.Analyzers.CSharpDoNotMarkServicedComponentsWithWebMethodFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetFramework.VisualBasic.Analyzers.BasicDoNotMarkServicedComponentsWithWebMethodAnalyzer,
    Microsoft.NetFramework.VisualBasic.Analyzers.BasicDoNotMarkServicedComponentsWithWebMethodFixer>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class DoNotMarkServicedComponentsWithWebMethodTests
    {
    }
}