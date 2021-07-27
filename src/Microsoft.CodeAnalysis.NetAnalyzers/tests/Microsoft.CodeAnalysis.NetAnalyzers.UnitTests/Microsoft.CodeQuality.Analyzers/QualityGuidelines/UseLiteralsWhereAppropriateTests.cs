// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpUseLiteralsWhereAppropriate,
    Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines.CSharpUseLiteralsWhereAppropriateFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicUseLiteralsWhereAppropriate,
    Microsoft.CodeQuality.VisualBasic.Analyzers.QualityGuidelines.BasicUseLiteralsWhereAppropriateFixer>;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.UnitTests
{
    public class UseLiteralsWhereAppropriateTests
    {
        [Fact]
        public async Task CA1802_Diagnostics_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    static readonly string f1 = """";
    static readonly string f2 = ""Nothing"";
    static readonly string f3,f4 = ""Message is shown only for f4"";
    static readonly int f5 = 3;
    const int f6 = 3;
    static readonly int f7 = 8 + f6;
    internal static readonly int f8 = 8 + f6;
}",
                GetCSharpEmptyStringResultAt(line: 4, column: 28, symbolName: "f1"),
                GetCSharpDefaultResultAt(line: 5, column: 28, symbolName: "f2"),
                GetCSharpDefaultResultAt(line: 6, column: 31, symbolName: "f4"),
                GetCSharpDefaultResultAt(line: 7, column: 25, symbolName: "f5"),
                GetCSharpDefaultResultAt(line: 9, column: 25, symbolName: "f7"),
                GetCSharpDefaultResultAt(line: 10, column: 34, symbolName: "f8"));
        }

        [Fact]
        public async Task CA1802_NoDiagnostics_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Class1
{
    public static readonly string f1 = """"; // Not private or Internal
    static string f3, f4 = ""Message is shown only for f4""; // Not readonly
    readonly int f5 = 3; // Not static
    const int f6 = 3; // Is already const
    static int f9 = getF9();
    static readonly int f7 = 8 + f9; // f9 is not a const
    static readonly string f8 = null; // null value

    private static int getF9()
    {
        throw new System.NotImplementedException();
    }
}");
        }

        [Fact]
        public async Task CA1802_Diagnostics_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    Shared ReadOnly f1 As String = """"
    Shared ReadOnly f2 As String = ""Nothing""
    Shared ReadOnly f3 As String, f4 As String = ""Message is shown only for f4""
    Shared ReadOnly f5 As Integer = 3
    Const f6 As Integer = 3
    Shared ReadOnly f7 As Integer = 8 + f6
    Friend Shared ReadOnly f8 As Integer = 8 + f6
End Class",
                GetBasicEmptyStringResultAt(line: 3, column: 21, symbolName: "f1"),
                GetBasicDefaultResultAt(line: 4, column: 21, symbolName: "f2"),
                GetBasicDefaultResultAt(line: 5, column: 35, symbolName: "f4"),
                GetBasicDefaultResultAt(line: 6, column: 21, symbolName: "f5"),
                GetBasicDefaultResultAt(line: 8, column: 21, symbolName: "f7"),
                GetBasicDefaultResultAt(line: 9, column: 28, symbolName: "f8"));
        }

        [Fact]
        public async Task CA1802_NoDiagnostics_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Class1
    ' Not Private or Friend
    Public Shared ReadOnly f1 As String = """"
    ' Not Readonly
    Shared f3 As String, f4 As String = ""Message is shown only for f4""
    ' Not Shared
    ReadOnly f5 As Integer = 3
    ' Is already Const
    Const f6 As Integer = 3
    Shared f9 As Integer = getF9()
    ' f9 is not a Const
    Shared ReadOnly f7 As Integer = 8 + f9
    ' null value
    Shared ReadOnly f8 As String = Nothing

    Private Shared Function getF9() As Integer
        Throw New System.NotImplementedException()
    End Function
End Class");
        }

        [Theory]
        [WorkItem(2772, "https://github.com/dotnet/roslyn-analyzers/issues/2772")]
        [InlineData("", false)]
        [InlineData("dotnet_code_quality.required_modifiers = static", false)]
        [InlineData("dotnet_code_quality.required_modifiers = none", true)]
        [InlineData("dotnet_code_quality." + UseLiteralsWhereAppropriateAnalyzer.RuleId + ".required_modifiers = none", true)]
        public async Task EditorConfigConfiguration_RequiredModifiersOption(string editorConfigText, bool reportDiagnostic)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (reportDiagnostic)
            {
                expected = new[]
                {
                    GetCSharpDefaultResultAt(4, 26, "field")
                };
            }

            var csTest = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class Test
{
    private readonly int field = 0;
}
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            };
            csTest.ExpectedDiagnostics.AddRange(expected);
            await csTest.RunAsync();

            expected = Array.Empty<DiagnosticResult>();
            if (reportDiagnostic)
            {
                expected = new[]
                {
                    GetBasicDefaultResultAt(3, 22, "field")
                };
            }

            var vbTest = new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class Test
    Private ReadOnly field As Integer = 0
End Class
"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            };
            vbTest.ExpectedDiagnostics.AddRange(expected);
            await vbTest.RunAsync();
        }

        [Fact]
        public async Task CA1802_CSharp_IntPtr_UIntPtr_NoDiagnostic()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;

public class Class1
{
	internal static readonly IntPtr field1 = (nint)0;
	internal static readonly UIntPtr field2 = (nuint)0;
}",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1802_CSharp_nint_Diagnostic()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;

public class Class1
{
    internal static readonly nint field = (nint)0;
}",
                ExpectedDiagnostics =
                {
                    GetCSharpDefaultResultAt(6, 35, "field"),
                },
                FixedCode = @"
using System;

public class Class1
{
    internal const nint field = (nint)0;
}",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1802_CSharp_nuint_Diagnostic()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestCode = @"
using System;

public class Class1
{
    internal static readonly nuint field = (nuint)0;
}",
                ExpectedDiagnostics =
                {
                    GetCSharpDefaultResultAt(6, 36, "field"),
                },
                FixedCode = @"
using System;

public class Class1
{
    internal const nuint field = (nuint)0;
}",
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpDefaultResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetCSharpEmptyStringResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.EmptyStringRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetBasicDefaultResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.DefaultRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);

        private static DiagnosticResult GetBasicEmptyStringResultAt(int line, int column, string symbolName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(UseLiteralsWhereAppropriateAnalyzer.EmptyStringRule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(symbolName);
    }
}
