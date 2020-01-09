// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.NormalizeStringsToUppercaseAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpNormalizeStringsToUppercaseFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.NormalizeStringsToUppercaseAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicNormalizeStringsToUppercaseFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class NormalizeStringsToUppercaseTests
    {
        #region No Diagnostic Tests

        [Fact]
        public async Task NoDiagnostic_ToUpperCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethodOneA()
    {
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.InvariantCulture));
    }

    public void TestMethodOneB()
    {
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.CurrentCulture));
    }

    public void TestMethodOneC()
    {
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.CurrentUICulture));
    }

    public void TestMethodOneD()
    {
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.InstalledUICulture));
    }

    public void TestMethodOneE()
    {
        var dynamicCulture = CultureInfo.CurrentCulture;
        Console.WriteLine(""FOO"".ToUpper(dynamicCulture));
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethodOneA()
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.InvariantCulture))
    End Sub

    Public Sub TestMethodOneB()
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.CurrentCulture))
    End Sub

    Public Sub TestMethodOneC()
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.CurrentUICulture))
    End Sub

    Public Sub TestMethodOneD()
        Console.WriteLine(""FOO"".ToUpper(CultureInfo.InstalledUICulture))
    End Sub

    Public Sub TestMethodOneE()
        Dim dynamicCulture = CultureInfo.CurrentCulture
        Console.WriteLine(""FOO"".ToUpper(dynamicCulture))
    End Sub
End Class
");
        }

        [Fact]
        public async Task NoDiagnostic_ToLowerCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethodTwoA()
    {
        Console.WriteLine(""FOO"".ToLower());
    }

    public void TestMethodTwoB()
    {
        Console.WriteLine(""FOO"".ToLower(CultureInfo.CurrentCulture));
    }

    public void TestMethodTwoC()
    {
        Console.WriteLine(""FOO"".ToLower(CultureInfo.CurrentUICulture));
    }

    public void TestMethodTwoD()
    {
        Console.WriteLine(""FOO"".ToLower(CultureInfo.InstalledUICulture));
    }

    public void TestMethodTwoE()
    {
        var dynamicCulture = CultureInfo.CurrentCulture;
        Console.WriteLine(""FOO"".ToLower(dynamicCulture));
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethodTwoA()
        Console.WriteLine(""FOO"".ToLower())
    End Sub

    Public Sub TestMethodTwoB()
        Console.WriteLine(""FOO"".ToLower(CultureInfo.CurrentCulture))
    End Sub

    Public Sub TestMethodTwoC()
        Console.WriteLine(""FOO"".ToLower(CultureInfo.CurrentUICulture))
    End Sub

    Public Sub TestMethodTwoD()
        Console.WriteLine(""FOO"".ToLower(CultureInfo.InstalledUICulture))
    End Sub

    Public Sub TestMethodTwoE()
        Dim dynamicCulture = CultureInfo.CurrentCulture
        Console.WriteLine(""FOO"".ToLower(dynamicCulture))
    End Sub
End Class
");
        }

        [Fact]
        public async Task NoDiagnostic_ToUpperInvariantCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethodThree()
    {
        Console.WriteLine(""FOO"".ToUpperInvariant());
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethodThree()
        Console.WriteLine(""FOO"".ToUpperInvariant())
    End Sub
End Class
");
        }

        #endregion

        #region Diagnostic Tests

        [Fact]
        public async Task Diagnostic_ToLowerCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethod()
    {
        Console.WriteLine(""FOO"".ToLower(CultureInfo.InvariantCulture));
    }
}
",
            GetCSharpDefaultResultAt(9, 27, "TestMethod", "ToLower", "ToUpperInvariant"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethod()
        Console.WriteLine(""FOO"".ToLower(CultureInfo.InvariantCulture))
    End Sub
End Class
",
            GetBasicDefaultResultAt(7, 27, "TestMethod", "ToLower", "ToUpperInvariant"));
        }

        [Fact]
        public async Task Diagnostic_ToLowerInvariantCases()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethod()
    {
        Console.WriteLine(""FOO"".ToLowerInvariant());
    }
}
",
            GetCSharpDefaultResultAt(9, 27, "TestMethod", "ToLowerInvariant", "ToUpperInvariant"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethod()
        Console.WriteLine(""FOO"".ToLowerInvariant())
    End Sub
End Class
",
            GetBasicDefaultResultAt(7, 27, "TestMethod", "ToLowerInvariant", "ToUpperInvariant"));
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCSharpDefaultResultAt(int line, int column, string containingMethod, string invokedMethod, string suggestedMethod)
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(containingMethod, invokedMethod, suggestedMethod);

        private static DiagnosticResult GetBasicDefaultResultAt(int line, int column, string containingMethod, string invokedMethod, string suggestedMethod)
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
                .WithArguments(containingMethod, invokedMethod, suggestedMethod);

        #endregion
    }
}