// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RethrowToPreserveStackDetailsAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RethrowToPreserveStackDetailsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RethrowToPreserveStackDetailsAnalyzer,
    Microsoft.CodeQuality.Analyzers.QualityGuidelines.RethrowToPreserveStackDetailsFixer>;

namespace Microsoft.CodeQuality.Analyzers.UnitTests.QualityGuidelines
{
    public class RethrowToPreserveStackDetailsTests
    {
        [Fact]
        public async Task TestCSharp_RethrowExplicitlyToThrowImplicitlyAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
#pragma warning disable RS0030 // Do not used banned APIs
@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            ThrowException();
        }
        catch (ArithmeticException e)
        {
            throw e; //Some comments
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}", VerifyCS.Diagnostic().WithLocation(14, 13),
#pragma warning restore RS0030 // Do not used banned APIs
@"
using System;

class Program
{
    void CatchAndRethrowExplicitly()
    {
        try
        {
            ThrowException();
        }
        catch (ArithmeticException e)
        {
            throw; //Some comments
        }
    }

    void ThrowException()
    {
        throw new ArithmeticException();
    }
}");
        }
        [Fact]
        public async Task TestBasic_RethrowExplicitlyToThrowImplicitlyAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(
#pragma warning disable RS0030 // Do not used banned APIs
@"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()
        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            Throw e 'Some comment
        End Try
    End Sub
End Class
", VerifyVB.Diagnostic().WithLocation(8, 13),
#pragma warning restore RS0030 // Do not used banned APIs
    @"
Imports System
Class Program
    Sub CatchAndRethrowExplicitly()
        Try
            Throw New ArithmeticException()
        Catch e As ArithmeticException
            Throw 'Some comment
        End Try
    End Sub
End Class
"
    );
        }
    }
}

