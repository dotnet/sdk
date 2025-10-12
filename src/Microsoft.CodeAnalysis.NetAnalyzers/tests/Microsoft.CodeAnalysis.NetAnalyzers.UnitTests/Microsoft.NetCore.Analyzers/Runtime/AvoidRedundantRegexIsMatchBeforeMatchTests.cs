// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidRedundantRegexIsMatchBeforeMatch,
    Microsoft.NetCore.Analyzers.Runtime.AvoidRedundantRegexIsMatchBeforeMatchFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.AvoidRedundantRegexIsMatchBeforeMatch,
    Microsoft.NetCore.Analyzers.Runtime.AvoidRedundantRegexIsMatchBeforeMatchFixer>;

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
        public async Task RedundantIsMatchGuard_InvertedWithEarlyReturn_CSharp_ReportsDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync("""
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (!{|CA2027:Regex.IsMatch(input, pattern)|})
                        {
                            return;
                        }

                        Match m = Regex.Match(input, pattern);
                        // use m
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
    }
}
