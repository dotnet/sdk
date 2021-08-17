// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForNaNCorrectlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpTestForNaNCorrectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.TestForNaNCorrectlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicTestForNaNCorrectlyFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class TestForNaNCorrectlyFixerTests
    {
        [Fact]
        public async Task CA2242_FixFloatForEqualityWithFloatNaN()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(float f)
    {
        return [|f == float.NaN|];
    }
}
", @"
public class A
{
    public bool Compare(float f)
    {
        return float.IsNaN(f);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As Single) As Boolean
        Return [|s = Single.NaN|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As Single) As Boolean
        Return Single.IsNaN(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixFloatForInequalityWithFloatNaN()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(float f)
    {
        return [|f != float.NaN|];
    }
}
", @"
public class A
{
    public bool Compare(float f)
    {
        return !float.IsNaN(f);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As Single) As Boolean
        Return [|s <> Single.NaN|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As Single) As Boolean
        Return Not Single.IsNaN(s)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixDoubleForEqualityWithDoubleNaN()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(double d)
    {
        return [|d == double.NaN|];
    }
}
", @"
public class A
{
    public bool Compare(double d)
    {
        return double.IsNaN(d);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(d As Double) As Boolean
        Return [|d = Double.NaN|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(d As Double) As Boolean
        Return Double.IsNaN(d)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixDoubleForInequalityWithDoubleNaN()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(double d)
    {
        return [|d != double.NaN|];
    }
}
", @"
public class A
{
    public bool Compare(double d)
    {
        return !double.IsNaN(d);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(d As Double) As Boolean
        Return [|d <> Double.NaN|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(d As Double) As Boolean
        Return Not Double.IsNaN(d)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNOnLeft()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare(double d)
    {
        return [|double.NaN == d|];
    }
}
", @"
public class A
{
    public bool Compare(double d)
    {
        return double.IsNaN(d);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare(s As Single) As Boolean
        Return [|Single.NaN = s|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare(s As Single) As Boolean
        Return Single.IsNaN(s
)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixOnlyOneDiagnosticForComparisonWithNaNOnBothSides()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    public bool Compare()
    {
        return [|float.NaN == float.NaN|];
    }
}
", @"
public class A
{
    public bool Compare()
    {
        return float.IsNaN(float.NaN);
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Public Function Compare() As Boolean
        Return [|Double.NaN = Double.NaN|]
    End Function
End Class
", @"
Public Class A
    Public Function Compare() As Boolean
        Return Double.IsNaN(Double.NaN
)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInFunctionArgument()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        G([|_n == float.NaN|]);
    }

    public void G(bool comparison) {}
}
", @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        G(float.IsNaN(_n));
    }

    public void G(bool comparison) {}
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Private _n As Single = 42.0F

    Public Sub F()
        G([|_n = Single.NaN|])
    End Sub

    Public Sub G(comparison As Boolean)
    End Sub
End Class
", @"
Public Class A
    Private _n As Single = 42.0F

    Public Sub F()
        G(Single.IsNaN(_n))
    End Sub

    Public Sub G(comparison As Boolean)
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInTernaryOperator()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public int F()
    {
        return [|_n == float.NaN|] ? 1 : 0;
    }
}
", @"
public class A
{
    float _n = 42.0F;

    public int F()
    {
        return float.IsNaN(_n) ? 1 : 0;
    }
}
");

            // VB doesn't have the ternary operator, but we add this test for symmetry.
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class A
    Private _n As Single = 42.0F

    Public Function F() As Integer
        Return If([|_n = Single.NaN|], 1, 0)
    End Function
End Class
", @"
Public Class A
    Private _n As Single = 42.0F

    Public Function F() As Integer
        Return If(Single.IsNaN(_n), 1, 0)
    End Function
End Class
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInThrowStatement()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        throw [|_n != float.NaN|] ? new System.Exception() : new System.ArgumentException();
    }
}
", @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        throw !float.IsNaN(_n) ? new System.Exception() : new System.ArgumentException();
    }
}
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInCatchFilterClause()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
public class A
{
    float _n = 42.0F;

    public void F()
    {
        try { }
        catch (Exception ex) when ([|_n != float.NaN|]) { }
    }
}
", @"
using System;
public class A
{
    float _n = 42.0F;

    public void F()
    {
        try { }
        catch (Exception ex) when (!float.IsNaN(_n)) { }
    }
}
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInYieldReturnStatement()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.Collections.Generic;

public class A
{
    float _n = 42.0F;

    public IEnumerable<bool> F()
    {
        yield return [|_n != float.NaN|];
    }
}
", @"
using System.Collections.Generic;

public class A
{
    float _n = 42.0F;

    public IEnumerable<bool> F()
    {
        yield return !float.IsNaN(_n);
    }
}
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInSwitchStatement()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        switch ([|_n != float.NaN|])
        {
            default:
                throw new System.NotImplementedException();
        }
    }
}
", @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        switch (!float.IsNaN(_n))
        {
            default:
                throw new System.NotImplementedException();
        }
    }
}
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInForLoop()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        for (; [|_n != float.NaN|]; )
        {
            throw new System.Exception();
        }
    }
}
", @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        for (; !float.IsNaN(_n); )
        {
            throw new System.Exception();
        }
    }
}
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInWhileLoop()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        while ([|_n != float.NaN|])
        {
        }
    }
}
", @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        while (!float.IsNaN(_n))
        {
        }
    }
}
");
        }

        [Fact]
        public async Task CA2242_FixForComparisonWithNaNInDoWhileLoop()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        do
        {
        }
        while ([|_n != float.NaN|]);
    }
}
", @"
public class A
{
    float _n = 42.0F;

    public void F()
    {
        do
        {
        }
        while (!float.IsNaN(_n));
    }
}
");
        }
    }
}