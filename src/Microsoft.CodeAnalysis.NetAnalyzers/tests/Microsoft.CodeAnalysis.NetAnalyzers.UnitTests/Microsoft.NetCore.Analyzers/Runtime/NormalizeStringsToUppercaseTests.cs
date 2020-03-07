// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class NormalizeStringsToUppercaseTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new NormalizeStringsToUppercaseAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NormalizeStringsToUppercaseAnalyzer();
        }

        #region No Diagnostic Tests

        [Fact]
        public void NoDiagnostic_ToUpperCases()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void NoDiagnostic_ToLowerCases()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void NoDiagnostic_ToUpperInvariantCases()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void Diagnostic_ToLowerCases()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void Diagnostic_ToLowerInvariantCases()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        {
            // In method '{0}', replace the call to '{1}' with '{2}'.
            string message = string.Format(NormalizeStringsToUppercaseAnalyzer.ToUpperRule.MessageFormat.ToString(CultureInfo.CurrentUICulture), containingMethod, invokedMethod, suggestedMethod);
            return GetCSharpResultAt(line, column, NormalizeStringsToUppercaseAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetBasicDefaultResultAt(int line, int column, string containingMethod, string invokedMethod, string suggestedMethod)
        {
            // In method '{0}', replace the call to '{1}' with '{2}'.
            string message = string.Format(NormalizeStringsToUppercaseAnalyzer.ToUpperRule.MessageFormat.ToString(CultureInfo.CurrentUICulture), containingMethod, invokedMethod, suggestedMethod);
            return GetBasicResultAt(line, column, NormalizeStringsToUppercaseAnalyzer.RuleId, message);
        }

        #endregion
    }
}