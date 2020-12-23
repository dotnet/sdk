// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AbstractTypesShouldNotHaveConstructorsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class CA1012Tests
    {
        [Fact]
        public async Task TestCSAbstractClassWithProtectedConstructor()
        {
            var code = @"
public abstract class C
{
    protected C()
    {
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task TestVBAbstractClassWithProtectedConstructor()
        {
            var code = @"
Public MustInherit Class C
    Protected Sub New()
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }
    }
}
