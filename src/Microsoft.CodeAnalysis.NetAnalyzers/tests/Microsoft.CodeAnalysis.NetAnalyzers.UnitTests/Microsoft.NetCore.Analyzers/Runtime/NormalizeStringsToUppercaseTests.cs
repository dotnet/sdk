// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
        public async Task NoDiagnostic_ToUpperCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethodOneA()
    {
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.InvariantCulture));
    }

    public void TestMethodOneB()
    {
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.CurrentCulture));
    }

    public void TestMethodOneC()
    {
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.CurrentUICulture));
    }

    public void TestMethodOneD()
    {
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.InstalledUICulture));
    }

    public void TestMethodOneE()
    {
        var dynamicCulture = CultureInfo.CurrentCulture;
        Console.WriteLine(""AAA"".ToUpper(dynamicCulture));
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethodOneA()
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.InvariantCulture))
    End Sub

    Public Sub TestMethodOneB()
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.CurrentCulture))
    End Sub

    Public Sub TestMethodOneC()
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.CurrentUICulture))
    End Sub

    Public Sub TestMethodOneD()
        Console.WriteLine(""AAA"".ToUpper(CultureInfo.InstalledUICulture))
    End Sub

    Public Sub TestMethodOneE()
        Dim dynamicCulture = CultureInfo.CurrentCulture
        Console.WriteLine(""AAA"".ToUpper(dynamicCulture))
    End Sub
End Class
");
        }

        [Fact]
        public async Task NoDiagnostic_ToLowerCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethodTwoA()
    {
        Console.WriteLine(""AAA"".ToLower());
    }

    public void TestMethodTwoB()
    {
        Console.WriteLine(""AAA"".ToLower(CultureInfo.CurrentCulture));
    }

    public void TestMethodTwoC()
    {
        Console.WriteLine(""AAA"".ToLower(CultureInfo.CurrentUICulture));
    }

    public void TestMethodTwoD()
    {
        Console.WriteLine(""AAA"".ToLower(CultureInfo.InstalledUICulture));
    }

    public void TestMethodTwoE()
    {
        var dynamicCulture = CultureInfo.CurrentCulture;
        Console.WriteLine(""AAA"".ToLower(dynamicCulture));
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethodTwoA()
        Console.WriteLine(""AAA"".ToLower())
    End Sub

    Public Sub TestMethodTwoB()
        Console.WriteLine(""AAA"".ToLower(CultureInfo.CurrentCulture))
    End Sub

    Public Sub TestMethodTwoC()
        Console.WriteLine(""AAA"".ToLower(CultureInfo.CurrentUICulture))
    End Sub

    Public Sub TestMethodTwoD()
        Console.WriteLine(""AAA"".ToLower(CultureInfo.InstalledUICulture))
    End Sub

    Public Sub TestMethodTwoE()
        Dim dynamicCulture = CultureInfo.CurrentCulture
        Console.WriteLine(""AAA"".ToLower(dynamicCulture))
    End Sub
End Class
");
        }

        [Fact]
        public async Task NoDiagnostic_ToUpperInvariantCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethodThree()
    {
        Console.WriteLine(""AAA"".ToUpperInvariant());
    }
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethodThree()
        Console.WriteLine(""AAA"".ToUpperInvariant())
    End Sub
End Class
");
        }

        #endregion

        #region Diagnostic Tests

        [Fact]
        public async Task Diagnostic_ToLowerCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethod()
    {
        Console.WriteLine(""AAA"".ToLower(CultureInfo.InvariantCulture));
    }
}
",
            GetCSharpDefaultResultAt(9, 27, "TestMethod", "ToLower", "ToUpperInvariant"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethod()
        Console.WriteLine(""AAA"".ToLower(CultureInfo.InvariantCulture))
    End Sub
End Class
",
            GetBasicDefaultResultAt(7, 27, "TestMethod", "ToLower", "ToUpperInvariant"));
        }

        [Fact]
        public async Task Diagnostic_ToLowerInvariantCasesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

public class NormalizeStringsTesterClass
{
    public void TestMethod()
    {
        Console.WriteLine(""AAA"".ToLowerInvariant());
    }
}
",
            GetCSharpDefaultResultAt(9, 27, "TestMethod", "ToLowerInvariant", "ToUpperInvariant"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Public Class NormalizeStringsTesterClass
    Public Sub TestMethod()
        Console.WriteLine(""AAA"".ToLowerInvariant())
    End Sub
End Class
",
            GetBasicDefaultResultAt(7, 27, "TestMethod", "ToLowerInvariant", "ToUpperInvariant"));
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCSharpDefaultResultAt(int line, int column, string containingMethod, string invokedMethod, string suggestedMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(containingMethod, invokedMethod, suggestedMethod);

        private static DiagnosticResult GetBasicDefaultResultAt(int line, int column, string containingMethod, string invokedMethod, string suggestedMethod)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(containingMethod, invokedMethod, suggestedMethod);

        #endregion
    }
}