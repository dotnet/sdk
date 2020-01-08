// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpVariableNamesShouldNotMatchFieldNamesAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.Maintainability.CSharpVariableNamesShouldNotMatchFieldNamesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicVariableNamesShouldNotMatchFieldNamesAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.Maintainability.BasicVariableNamesShouldNotMatchFieldNamesFixer>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class VariableNamesShouldNotMatchFieldNamesTests
    {
    }
}