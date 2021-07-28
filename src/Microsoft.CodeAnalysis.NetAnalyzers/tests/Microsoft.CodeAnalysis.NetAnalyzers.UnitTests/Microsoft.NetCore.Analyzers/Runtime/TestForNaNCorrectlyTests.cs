// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForNaNCorrectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForNaNCorrectlyAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class TestForNaNCorrectlyTests
    {
        [Fact]
        public async Task CSharpDiagnosticForEqualityWithFloatNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f == float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForEqualityWithFloatNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f = Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForInequalityWithFloatNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f != float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForInEqualityWithFloatNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f <> Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForGreaterThanFloatNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f > float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForGreaterThanFloatNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f > Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForGreaterThanOrEqualToFloatNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f >= float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForGreaterThanOrEqualToFloatNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f >= Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForLessThanFloatNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f < float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForLessThanFloatNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f < Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForLessThanOrEqualToFloatNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f <= float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForLessThanOrEqualToFloatNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f <= Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithDoubleNaN()
        {
            var code = @"
public class A
{
    public bool Compare(double d)
    {
        return d == double.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForComparisonWithDoubleNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(d As Double) As Boolean
        Return d < Double.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNOnLeft()
        {
            var code = @"
public class A
{
    public bool Compare(double d)
    {
        return double.NaN == d;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForComparisonWithNaNOnLeft()
        {
            var code = @"
Public Class A
    Public Function Compare(d As Double) As Boolean
        Return Double.NaN = d
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForComparisonWithBadExpression()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f == float.{|CS0117:NbN|}; // Misspelled.
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticForComparisonWithBadExpression()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f = {|BC30456:Single.NbN|}   ' Misspelled
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticForComparisonWithFunctionReturningNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f == NaNFunc();
    }

    private float NaNFunc()
    {
        return float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticForComparisonWithFunctionReturningNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f = NaNFunc()
    End Function

    Private Function NaNFunc() As Single
        Return Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticForEqualityWithNonNaN()
        {
            var code = @"
public class A
{
    public bool Compare(float f)
    {
        return f == 1.0;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticForEqualityWithNonNaN()
        {
            var code = @"
Public Class A
    Public Function Compare(f As Single) As Boolean
        Return f = 1.0
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpNoDiagnosticForNonComparisonOperationWithNaN()
        {
            var code = @"
public class A
{
    public float OperateOn(float f)
    {
        return f + float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task BasicNoDiagnosticForNonComparisonOperationWithNonNaN()
        {
            var code = @"
Public Class A
    Public Function OperateOn(f As Single) As Single
        Return f + Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public async Task CSharpOnlyOneDiagnosticForComparisonWithNaNOnBothSides()
        {
            var code = @"
public class A
{
    public bool Compare()
    {
        return float.NaN == float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(6, 16));
        }

        [Fact]
        public async Task BasicOnlyOneDiagnosticForComparisonWithNonNaNOnBothSides()
        {
            var code = @"
Public Class A
    Public Function Compare() As Boolean
        Return Single.NaN = Single.NaN
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(4, 16));
        }

        // At @srivatsn's suggestion, here are a few tests that verify that the operation
        // tree is correct when the comparison occurs in syntactic constructs other than
        // a function return value. Of course we can't be exhaustive about this, and these
        // tests are really more about the correctness of the operation tree -- ensuring
        // that "binary operator expressions" are present in places we expect them to be --
        // than they are about the correctness of our treatment of these expressions once
        // we find them.
        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInFunctionArgument()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        G(_n == float.NaN);
    }

    public void G(bool comparison) {}
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(8, 11));
        }

        [Fact]
        public async Task BasicDiagnosticForComparisonWithNaNInFunctionArgument()
        {
            var code = @"
Public Class A
    Private _n As Single = 42.0F

    Public Sub F()
        G(_n = Single.NaN)
    End Sub

    Public Sub G(comparison As Boolean)
    End Sub
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(6, 11));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInTernaryOperator()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public int F()
    {
        return _n == float.NaN ? 1 : 0;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(8, 16));
        }

        [Fact]
        public async Task BasicDiagnosticForComparisonWithNaNInIfOperator()
        {
            // VB doesn't have the ternary operator, but we add this test for symmetry.
            var code = @"
Public Class A
    Private _n As Single = 42.0F

    Public Function F() As Integer
        Return If(_n = Single.NaN, 1, 0)
    End Function
End Class
";
            await VerifyVB.VerifyAnalyzerAsync(code, GetBasicResultAt(6, 19));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInThrowStatement()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        throw _n != float.NaN ? new System.Exception() : new System.ArgumentException();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(8, 15));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInCatchFilterClause()
        {
            var code = @"
using System;

public class A
{
    float _n = 42.0F;

    public void F()
    {
        try
        {
        }
        catch (Exception ex) when (_n != float.NaN)
        {
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(13, 36));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInYieldReturnStatement()
        {
            var code = @"
using System.Collections.Generic;

public class A
{
    float _n = 42.0F;

    public IEnumerable<bool> F()
    {
        yield return _n != float.NaN;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(10, 22));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInSwitchStatement()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        switch (_n != float.NaN)
        {
            default:
                throw new System.NotImplementedException();
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(8, 17));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInForLoop()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        for (; _n != float.NaN; )
        {
            throw new System.Exception();
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(8, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInWhileLoop()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        while (_n != float.NaN)
        {
        }
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(8, 16));
        }

        [Fact]
        public async Task CSharpDiagnosticForComparisonWithNaNInDoWhileLoop()
        {
            var code = @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        do
        {
        }
        while (_n != float.NaN);
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(code, GetCSharpResultAt(11, 16));
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}