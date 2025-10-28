// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidRedundantRegexIsMatchBeforeMatch,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpAvoidRedundantRegexIsMatchBeforeMatchFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidRedundantRegexIsMatchBeforeMatch,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AvoidRedundantRegexIsMatchBeforeMatchTests
    {
        [Fact]
        public async Task RedundantIsMatchGuard_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern)|})
                        {
                            Match m = Regex.Match(input, pattern);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_InstanceMethod_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(Regex regex, string input)
                    {
                        if ({|CA2027:regex.IsMatch(input)|})
                        {
                            Match m = regex.Match(input);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_DifferentArguments_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input1, string input2, string pattern)
                    {
                        if (Regex.IsMatch(input1, pattern))
                        {
                            Match m = Regex.Match(input2, pattern);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_DifferentInstance_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(Regex regex1, Regex regex2, string input)
                    {
                        if (regex1.IsMatch(input))
                        {
                            Match m = regex2.Match(input);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_NoMatchCall_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern))
                        {
                            // Do something without calling Match
                            System.Console.WriteLine("Matched!");
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_VisualBasic_ReportsDiagnostic()
        {
            await VerifyVB.VerifyAnalyzerAsync("""
                Imports System.Text.RegularExpressions

                Class C
                    Sub M(input As String, pattern As String)
                        If {|CA2027:Regex.IsMatch(input, pattern)|} Then
                            Dim m As Match = Regex.Match(input, pattern)
                            ' use m
                        End If
                    End Sub
                End Class
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_WithOptions_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase)|})
                        {
                            Match m = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_NestedInBlock_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern)|})
                        {
                            System.Console.WriteLine("Found match");
                            Match m = Regex.Match(input, pattern);
                            System.Console.WriteLine(m.Value);
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_InlineMatchUsage_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern)|})
                        {
                            System.Console.WriteLine(Regex.Match(input, pattern).Value);
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_MatchInElseBranch_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern))
                        {
                            System.Console.WriteLine("Matched");
                        }
                        else
                        {
                            Match m = Regex.Match(input, pattern);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_DifferentOptions_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                        {
                            Match m = Regex.Match(input, pattern, RegexOptions.Compiled);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_VariableReassigned_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern))
                        {
                            pattern = "different";
                            Match m = Regex.Match(input, pattern);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_PropertyArgument_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    string Pattern { get; set; } = "";

                    void M(string input)
                    {
                        if (Regex.IsMatch(input, Pattern))
                        {
                            Match m = Regex.Match(input, Pattern);
                            // use m - but Pattern could have changed
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_ConstantPattern_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2027:Regex.IsMatch(input, "\\d+")|})
                        {
                            Match m = Regex.Match(input, "\\d+");
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_DifferentInput_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input1, string input2, string pattern)
                    {
                        if (Regex.IsMatch(input1, pattern))
                        {
                            Match m = Regex.Match(input2, pattern);
                            // use m - different input
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_DifferentPattern_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern1, string pattern2)
                    {
                        if (Regex.IsMatch(input, pattern1))
                        {
                            Match m = Regex.Match(input, pattern2);
                            // use m - different pattern
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_InputReassignedInBlock_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern))
                        {
                            input = "changed";
                            Match m = Regex.Match(input, pattern);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_FieldInstance_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    Regex _regex = new Regex("\\d+");

                    void M(string input)
                    {
                        if (_regex.IsMatch(input))
                        {
                            Match m = _regex.Match(input);
                            // use m - field could be reassigned
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_ReadOnlyFieldInstance_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    readonly Regex _regex = new Regex("\\d+");

                    void M(string input)
                    {
                        if ({|CA2027:_regex.IsMatch(input)|})
                        {
                            Match m = _regex.Match(input);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_InstanceReassignedInBlock_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(Regex regex, string input)
                    {
                        if (regex.IsMatch(input))
                        {
                            regex = new Regex("different");
                            Match m = regex.Match(input);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_ConditionalEarlyExit_CSharp_ReportsDiagnostic()
        {
            // Note: This is a conservative false positive - the Match may not always be reached
            // but the analyzer reports it anyway to keep the logic simple
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern, bool condition)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern)|})
                        {
                            if (condition)
                                return;
                            Match m = Regex.Match(input, pattern);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_MultipleStatementsInBlock_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern)|})
                        {
                            System.Console.WriteLine("Found");
                            Match m = Regex.Match(input, pattern);
                            System.Console.WriteLine(m.Value);
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_WithRegexOptionsVariable_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern, RegexOptions options)
                    {
                        if ({|CA2027:Regex.IsMatch(input, pattern, options)|})
                        {
                            Match m = Regex.Match(input, pattern, options);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_DifferentRegexOptions_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                        {
                            Match m = Regex.Match(input, pattern, RegexOptions.Compiled);
                            // use m - different options
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task RedundantIsMatchGuard_LocalRegexVariable_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        Regex regex = new Regex("\\d+");
                        if ({|CA2027:regex.IsMatch(input)|})
                        {
                            Match m = regex.Match(input);
                            // use m
                        }
                    }
                }
                """);
        }

        [Fact]
        public async Task NoRedundantIsMatchGuard_MatchInDifferentScope_CSharp_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.IsMatch(input, pattern))
                        {
                            System.Console.WriteLine("Match found");
                        }

                        // Match call outside the if block
                        Match m = Regex.Match(input, pattern);
                        System.Console.WriteLine(m.Value);
                    }
                }
                """);
        }

    }
}
