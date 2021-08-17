// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompare,
    Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompareFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompare,
    Microsoft.NetCore.Analyzers.Runtime.UseStringEqualsOverStringCompareFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseStringEqualsOverStringCompareTests
    {
        #region Test Data
        private static DiagnosticDescriptor Rule => UseStringEqualsOverStringCompare.Rule;

        private static readonly (string CompareCall, string EqualsCall)[] CS_ComparisonEqualityMethodCallPairs = new[]
        {
            ("string.Compare(x, y)", "string.Equals(x, y)"),
            ("string.Compare(x, y, false)", "string.Equals(x, y, StringComparison.CurrentCulture)"),
            ("string.Compare(x, y, true)", "string.Equals(x, y, StringComparison.CurrentCultureIgnoreCase)"),
            ("string.Compare(x, y, StringComparison.CurrentCulture)", "string.Equals(x, y, StringComparison.CurrentCulture)"),
            ("string.Compare(x, y, StringComparison.Ordinal)", "string.Equals(x, y, StringComparison.Ordinal)"),
            ("string.Compare(x, y, StringComparison.OrdinalIgnoreCase)", "string.Equals(x, y, StringComparison.OrdinalIgnoreCase)"),
        };

        private static readonly (string CompareCall, string EqualsCall)[] VB_ComparisonEqualityMethodPairs = new[]
        {
            ("String.Compare(x, y)", "String.Equals(x, y)"),
            ("String.Compare(x, y, false)", "String.Equals(x, y, StringComparison.CurrentCulture)"),
            ("String.Compare(x, y, true)", "String.Equals(x, y, StringComparison.CurrentCultureIgnoreCase)"),
            ("String.Compare(x, y, StringComparison.CurrentCulture)", "String.Equals(x, y, StringComparison.CurrentCulture)"),
            ("String.Compare(x, y, StringComparison.Ordinal)", "String.Equals(x, y, StringComparison.Ordinal)"),
            ("String.Compare(x, y, StringComparison.OrdinalIgnoreCase)", "String.Equals(x, y, StringComparison.OrdinalIgnoreCase)"),
        };

        public static IEnumerable<object[]> CS_ComparisonLeftOfLiteralTestData { get; } = CS_ComparisonEqualityMethodCallPairs
            .Select(pair => new object[] { $"{pair.CompareCall} == 0", pair.EqualsCall });

        public static IEnumerable<object[]> VB_ComparisonLeftOfLiteralTestData { get; } = VB_ComparisonEqualityMethodPairs
            .Select(pair => new object[] { $"{pair.CompareCall} = 0", pair.EqualsCall });

        public static IEnumerable<object[]> CS_ComparisonRightOfLiteralTestData { get; } = CS_ComparisonEqualityMethodCallPairs
            .Select(pair => new object[] { $"0 == {pair.CompareCall}", pair.EqualsCall });

        public static IEnumerable<object[]> VB_ComparisonRightOfLiteralTestData { get; } = VB_ComparisonEqualityMethodPairs
            .Select(pair => new object[] { $"0 = {pair.CompareCall}", pair.EqualsCall });

        public static IEnumerable<object[]> CS_InvertedComparisonLeftOfLiteralTestData { get; } = CS_ComparisonEqualityMethodCallPairs
            .Select(pair => new object[] { $"{pair.CompareCall} != 0", $"!{pair.EqualsCall}" });

        public static IEnumerable<object[]> VB_InvertedComparisonLeftOfLiteralTestData { get; } = VB_ComparisonEqualityMethodPairs
            .Select(pair => new object[] { $"{pair.CompareCall} <> 0", $"Not {pair.EqualsCall}" });

        public static IEnumerable<object[]> CS_InvertedComparisonRightOfLiteralTestData { get; } = CS_ComparisonEqualityMethodCallPairs
            .Select(pair => new object[] { $"0 != {pair.CompareCall}", $"!{pair.EqualsCall}" });

        public static IEnumerable<object[]> VB_InvertedComparisonRightOfLiteralTestData { get; } = VB_ComparisonEqualityMethodPairs
            .Select(pair => new object[] { $"0 <> {pair.CompareCall}", $"Not {pair.EqualsCall}" });

        public static IEnumerable<object[]> CS_StringCompareExpressionsTestData { get; } = CS_ComparisonEqualityMethodCallPairs
            .Select(pair => new object[] { pair.CompareCall });

        public static IEnumerable<object[]> VB_StringCompareExpressionsTestData { get; } = VB_ComparisonEqualityMethodPairs
            .Select(pair => new object[] { pair.CompareCall });

        public static IEnumerable<object[]> CS_IneligibleStringCompareOverloadTestData
        {
            get
            {
                yield return new[] { "string.Compare(x, y, true, System.Globalization.CultureInfo.InvariantCulture)" };
                yield return new[] { "string.Compare(x, y, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CompareOptions.None)" };
            }
        }

        public static IEnumerable<object[]> VB_IneligibleStringCompareOverloadTestData
        {
            get
            {
                yield return new[] { "String.Compare(x, y, true, System.Globalization.CultureInfo.InvariantCulture)" };
                yield return new[] { "String.Compare(x, y, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CompareOptions.None)" };
            }
        }

        #endregion

        [Theory]
        [MemberData(nameof(CS_ComparisonLeftOfLiteralTestData))]
        [MemberData(nameof(CS_ComparisonRightOfLiteralTestData))]
        [MemberData(nameof(CS_InvertedComparisonLeftOfLiteralTestData))]
        [MemberData(nameof(CS_InvertedComparisonRightOfLiteralTestData))]
        public Task StringCompareResult_CompareToZero_Diagnostic_CS(string testExpression, string fixedExpression)
        {
            string testCode = $@"
using System;

public class Testopolis
{{
    public bool Huh(string x, string y)
    {{
        return {{|#0:{testExpression}|}};
    }}
}}";
            string fixedCode = $@"
using System;

public class Testopolis
{{
    public bool Huh(string x, string y)
    {{
        return {fixedExpression};
    }}
}}";

            return VerifyCS.VerifyCodeFixAsync(testCode, VerifyCS.Diagnostic(Rule).WithLocation(0), fixedCode);
        }

        [Theory]
        [MemberData(nameof(VB_ComparisonLeftOfLiteralTestData))]
        [MemberData(nameof(VB_ComparisonRightOfLiteralTestData))]
        [MemberData(nameof(VB_InvertedComparisonLeftOfLiteralTestData))]
        [MemberData(nameof(VB_InvertedComparisonRightOfLiteralTestData))]
        public Task StringCompareResult_CompareToZero_Diagnostic_VB(string testExpression, string fixedExpression)
        {
            string testCode = $@"
Imports System

Public Class Testopolis

    Public Function Huh(x As String, y As String) As Boolean
        Return {{|#0:{testExpression}|}}
    End Function
End Class";
            string fixedCode = $@"
Imports System

Public Class Testopolis

    Public Function Huh(x As String, y As String) As Boolean
        Return {fixedExpression}
    End Function
End Class";

            return VerifyVB.VerifyCodeFixAsync(testCode, VerifyVB.Diagnostic(Rule).WithLocation(0), fixedCode);
        }

        [Theory]
        [MemberData(nameof(CS_StringCompareExpressionsTestData))]
        public Task StringCompareResult_CompareToNonLiteralZero_NoDiagnostic_CS(string expression)
        {
            string code = $@"
using System;

public class Testopolis
{{
    private const int Zero = 0;

    public void Method(string x, string y)
    {{
        bool a = {expression} == Zero;
        bool b = {expression} != Zero;
        bool c = Zero == {expression};
        bool d = Zero != {expression};
    }}
}}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(VB_StringCompareExpressionsTestData))]
        public Task StringCompareResult_CompareToNonLiteralZero_NoDiagnostic_VB(string expression)
        {
            var code = $@"
Imports System

Public Class Testopolis
    Private Const Zero As Integer = 0

    Public Sub Method(x As String, y As String)
        Dim a = {expression} = Zero
        Dim b = {expression} <> Zero
        Dim c = Zero = {expression}
        Dim d = Zero <> {expression}
    End Sub
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(CS_StringCompareExpressionsTestData))]
        public Task StringCompareResult_CompareToLiteralNonZero_NoDiagnostic_CS(string expression)
        {
            string code = $@"
using System;

public class Testopolis
{{
    public void Method(string x, string y)
    {{
        bool a = {expression} == 1;
        bool b = {expression} != 1;
        bool c = 1 == {expression};
        bool d = 1 != {expression};
    }}
}}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(VB_StringCompareExpressionsTestData))]
        public Task StringCompareResult_CompareToLiteralNonZero_NoDiagnostic_VB(string expression)
        {
            string code = $@"
Imports System

Public Class Testopolis
    Public Sub Method(x As String, y As String)
        Dim a = {expression} = 1
        Dim b = {expression} <> 1
        Dim c = 1 = {expression}
        Dim d = 1 <> {expression}
    End Sub
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(CS_IneligibleStringCompareOverloadTestData))]
        public Task IneligibleStringCompareOverload_NoDiagnostic_CS(string expression)
        {
            string code = $@"
using System;

public class Testopolis
{{
    public void Method(string x, string y)
    {{
        bool a = {expression} == 0;
        bool b = {expression} != 0;
        bool c = 0 == {expression};
        bool d = 0 != {expression};
    }}
}}";

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Theory]
        [MemberData(nameof(VB_IneligibleStringCompareOverloadTestData))]
        public Task IneligibleStringCompareOverload_NoDiagnostic_VB(string expression)
        {
            string code = $@"
Imports System

Public Class Testopolis
    Public Sub Method(x As String, y As String)
        Dim a = {expression} = 0
        Dim b = {expression} <> 0
        Dim c = 0 = {expression}
        Dim d = 0 <> {expression}
    End Sub
End Class";

            return VerifyVB.VerifyAnalyzerAsync(code);
        }
    }
}
