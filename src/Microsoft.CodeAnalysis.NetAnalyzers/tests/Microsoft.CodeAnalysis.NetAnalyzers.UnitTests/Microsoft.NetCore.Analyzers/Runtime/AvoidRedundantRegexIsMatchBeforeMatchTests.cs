// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
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
        /// <summary>
        /// Helper for code fix tests — property patterns require C# 8+, but the
        /// test infrastructure defaults to C# 7.3.
        /// </summary>
        private static async Task VerifyCodeFixCSharp9Async(string source, string fixedSource)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                LanguageVersion = LanguageVersion.CSharp9,
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        #region Diagnostic tests — should flag

        [Fact]
        public async Task StaticIsMatchThenMatch_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.Match(input, @"\d+") is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task InstanceIsMatchThenMatch_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex = new Regex(@"\d+");
                        if ({|CA2028:regex.IsMatch(input)|})
                        {
                            Match m = regex.Match(input);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex = new Regex(@"\d+");
                        if (regex.Match(input) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task StaticWithOptions_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+", RegexOptions.IgnoreCase)|})
                        {
                            Match m = Regex.Match(input, @"\d+", RegexOptions.IgnoreCase);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.Match(input, @"\d+", RegexOptions.IgnoreCase) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task MatchUsedInlineWithGroupsAccess_FlagsButNoFix()
        {
            // Real-world pattern from BiliDuang: regex.Match(x).Groups[1].Value
            // Analyzer should flag, but fixer shouldn't offer a fix (no local declaration).
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    string M(string input)
                    {
                        var regex = new Regex(@"charset=(.+)");
                        if ({|CA2028:regex.IsMatch(input)|})
                        {
                            return regex.Match(input).Groups[1].Value;
                        }
                        return null;
                    }
                }
                """;
            // No code fix — Match is used inline, not assigned to a local.
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchInExpressionStatement_FlagsButNoFix()
        {
            // Match is assigned to an existing variable, not a new declaration.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        Match m;
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchWithVarDeclaration_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            var m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.Match(input, @"\d+") is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task ReadonlyFieldAsReceiver_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    private readonly Regex _regex = new Regex(@"\d+");

                    void M(string input)
                    {
                        if ({|CA2028:_regex.IsMatch(input)|})
                        {
                            Match m = _regex.Match(input);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    private readonly Regex _regex = new Regex(@"\d+");

                    void M(string input)
                    {
                        if (_regex.Match(input) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task ParameterAsInput_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if ({|CA2028:Regex.IsMatch(input, pattern)|})
                        {
                            Match m = Regex.Match(input, pattern);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string pattern)
                    {
                        if (Regex.Match(input, pattern) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task ConstantPatternString_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, "hello")|})
                        {
                            Match m = Regex.Match(input, "hello");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchWithOtherStatementsInBody_Flags()
        {
            // Match is not the first statement — diagnostic fires but fixer
            // skips because moving Match into condition changes execution order.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Console.WriteLine("Found a match");
                            Match m = Regex.Match(input, @"\d+");
                            Console.WriteLine(m.Value);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task InstanceWithStartAtParameter_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex = new Regex(@"\d+");
                        if ({|CA2028:regex.IsMatch(input, 0)|})
                        {
                            Match m = regex.Match(input, 0);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region No-diagnostic tests — should NOT flag

        [Fact]
        public async Task DifferentInputVariables_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input1, string input2)
                    {
                        if (Regex.IsMatch(input1, @"\d+"))
                        {
                            Match m = Regex.Match(input2, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task DifferentPatterns_NoDiagnostic()
        {
            // Real-world false positive: microsoft/perfview CommandLineUtilities.cs pattern
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\s"))
                        {
                            Match m = Regex.Match(input, @"^(.*?)(\\\\.*)""(.*)");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task DifferentRegexInstances_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex1 = new Regex(@"\d+");
                        var regex2 = new Regex(@"\d+");
                        if (regex1.IsMatch(input))
                        {
                            Match m = regex2.Match(input);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task DifferentOptions_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+", RegexOptions.IgnoreCase))
                        {
                            Match m = Regex.Match(input, @"\d+", RegexOptions.None);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task StaticIsMatchInstanceMatch_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex = new Regex(@"\d+");
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            Match m = regex.Match(input);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NegatedIsMatch_NoDiagnostic()
        {
            // Inverted case — explicitly dropped per stephentoub's review.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    string M(string input)
                    {
                        if (!Regex.IsMatch(input, @"\d+"))
                        {
                            return input;
                        }
                        Match m = Regex.Match(input, @"\d+");
                        return m.Value;
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchInElseBranch_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                        }
                        else
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchInsideLambda_NoDiagnostic()
        {
            // The Match call is inside a lambda — different scope.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            Action a = () =>
                            {
                                Match m = Regex.Match(input, @"\d+");
                            };
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchInsideLocalFunction_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            void Local()
                            {
                                Match m = Regex.Match(input, @"\d+");
                            }
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MutableFieldReceiver_NoDiagnostic()
        {
            // Non-readonly field — could be reassigned between calls.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    private Regex _regex = new Regex(@"\d+");

                    void M(string input)
                    {
                        if (_regex.IsMatch(input))
                        {
                            Match m = _regex.Match(input);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MutableFieldAsArgument_NoDiagnostic()
        {
            // Field used as argument is not readonly — could be mutated.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    private string _input = "hello";

                    void M()
                    {
                        if (Regex.IsMatch(_input, @"\d+"))
                        {
                            Match m = Regex.Match(_input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task DifferentOverloads_NoDiagnostic()
        {
            // IsMatch with 2 args (input, pattern), Match with 3 args (input, pattern, options)
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            Match m = Regex.Match(input, @"\d+", RegexOptions.IgnoreCase);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task PropertyAsArgument_NoDiagnostic()
        {
            // Property access is not stable — could have side effects.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    string Input { get; set; }

                    void M()
                    {
                        if (Regex.IsMatch(Input, @"\d+"))
                        {
                            Match m = Regex.Match(Input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MethodCallAsArgument_NoDiagnostic()
        {
            // Method call result is not stable.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    string GetInput() => "hello";

                    void M()
                    {
                        if (Regex.IsMatch(GetInput(), @"\d+"))
                        {
                            Match m = Regex.Match(GetInput(), @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchNotInWhenTrue_NoDiagnostic()
        {
            // No Match call at all in the if body.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            Console.WriteLine("match found");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NotIsMatchCondition_NoDiagnostic()
        {
            // Condition is Regex.Match, not IsMatch.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.Match(input, @"\d+").Success)
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchInNestedIfBody_NoDiagnostic()
        {
            // Match is in a nested if block, not a direct child.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, bool flag)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            if (flag)
                            {
                                Match m = Regex.Match(input, @"\d+");
                            }
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NoRegexType_NoDiagnostic()
        {
            // When Regex type is not available.
            var source = """
                class C
                {
                    void M(string input)
                    {
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task InterveningLocalReassignment_NoDiagnostic()
        {
            // Local is reassigned between IsMatch and Match — values may differ.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            input = "something else";
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task InterveningParameterReassignment_NoDiagnostic()
        {
            // Parameter is reassigned between IsMatch and Match.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        string pattern = @"\d+";
                        if (Regex.IsMatch(input, pattern))
                        {
                            pattern = @"\w+";
                            Match m = Regex.Match(input, pattern);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task InterveningReceiverReassignment_NoDiagnostic()
        {
            // Instance receiver is reassigned between IsMatch and Match.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex = new Regex(@"\d+");
                        if (regex.IsMatch(input))
                        {
                            regex = new Regex(@"\w+");
                            Match m = regex.Match(input);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region Diagnostic but no fix

        [Fact]
        public async Task MatchNotFirstStatement_DiagnosticButNoFix()
        {
            // Match is not the first statement — diagnostic fires but fixer
            // skips to avoid changing execution order.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Console.WriteLine("before match");
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            // Verify diagnostic fires but no code fix changes (analyzer-only test).
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ElseBranchNameConflict_DiagnosticButNoFix()
        {
            // Else branch declares a variable with the same name as the Match
            // variable — fixer would introduce a name conflict.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                        else
                        {
                            int m = 0;
                            _ = m;
                        }
                    }
                }
                """;
            // Verify diagnostic fires but no code fix changes (analyzer-only test).
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region Real-world pattern variants

        [Fact]
        public async Task RealWorld_MatchUsedForGroupsInline()
        {
            // Based on BiliDuang Other.cs pattern:
            // if (regex.IsMatch(contentype)) { Encoding.GetEncoding(regex.Match(contentype).Groups[1].Value.Trim()); }
            var source = """
                using System;
                using System.Text;
                using System.Text.RegularExpressions;

                class C
                {
                    Encoding M(string contentType)
                    {
                        var regex = new Regex(@"charset=(.+)", RegexOptions.IgnoreCase);
                        if ({|CA2028:regex.IsMatch(contentType)|})
                        {
                            return Encoding.GetEncoding(regex.Match(contentType).Groups[1].Value.Trim());
                        }
                        return Encoding.UTF8;
                    }
                }
                """;
            // No fix — Match is used inline, not assigned to a local declaration.
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task RealWorld_MultiplePairsWithDifferentPatterns_NoDiagnostic()
        {
            // Based on OpenLiveWriter pattern: multiple IsMatch/Match pairs but with different patterns.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string chunk)
                    {
                        string blocks = "div|p|ul";
                        if (!Regex.IsMatch(chunk, @"^<\/?" + blocks))
                        {
                            if (!Regex.IsMatch(chunk, @"^<" + blocks))
                            {
                                // different pattern from the outer IsMatch
                            }
                            Match m = Regex.Match(chunk, @"<\/" + blocks + ">$");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region VB tests — analyzer only (no fixer)

        [Fact]
        public async Task VB_StaticIsMatchThenMatch_Flags()
        {
            var source = """
                Imports System.Text.RegularExpressions

                Class C
                    Sub M(input As String)
                        If {|CA2028:Regex.IsMatch(input, "\d+")|} Then
                            Dim m As Match = Regex.Match(input, "\d+")
                        End If
                    End Sub
                End Class
                """;
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task VB_DifferentInputs_NoDiagnostic()
        {
            var source = """
                Imports System.Text.RegularExpressions

                Class C
                    Sub M(input1 As String, input2 As String)
                        If Regex.IsMatch(input1, "\d+") Then
                            Dim m As Match = Regex.Match(input2, "\d+")
                        End If
                    End Sub
                End Class
                """;
            await VerifyVB.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region Edge cases

        [Fact]
        public async Task MultipleMatchCallsInBody_FlagsFirst()
        {
            // Only the first matching Match call should be included in the diagnostic.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Match m = Regex.Match(input, @"\d+");
                            Match m2 = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchReturnedFromIfBody_FlagsButNoFix()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    Match M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            return Regex.Match(input, @"\d+");
                        }
                        return null;
                    }
                }
                """;
            // Analyzer flags, but fixer doesn't handle return statements.
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task MatchInSingleStatementIfBody_Flags()
        {
            // No braces around if body.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                            Regex.Match(input, @"\d+");
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task StaticReadonlyFieldReceiver_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    private static readonly Regex s_regex = new Regex(@"\d+");

                    void M(string input)
                    {
                        if ({|CA2028:s_regex.IsMatch(input)|})
                        {
                            Match m = s_regex.Match(input);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    private static readonly Regex s_regex = new Regex(@"\d+");

                    void M(string input)
                    {
                        if (s_regex.Match(input) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task ParameterAsRegexReceiver_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(Regex regex, string input)
                    {
                        if ({|CA2028:regex.IsMatch(input)|})
                        {
                            Match m = regex.Match(input);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(Regex regex, string input)
                    {
                        if (regex.Match(input) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task IsMatchWithAdditionalConjunction_NoDiagnostic()
        {
            // Condition is IsMatch && something — not a simple IsMatch call.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, bool flag)
                    {
                        if (Regex.IsMatch(input, @"\d+") && flag)
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ConstFieldReceiver_Flags()
        {
            // const string used for pattern — equivalent.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    const string Pattern = @"\d+";

                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, Pattern)|})
                        {
                            Match m = Regex.Match(input, Pattern);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    const string Pattern = @"\d+";

                    void M(string input)
                    {
                        if (Regex.Match(input, Pattern) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task ReadonlyFieldAsPatternArgument_Flags()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    private readonly string _pattern = @"\d+";

                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, _pattern)|})
                        {
                            Match m = Regex.Match(input, _pattern);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    private readonly string _pattern = @"\d+";

                    void M(string input)
                    {
                        if (Regex.Match(input, _pattern) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        #endregion
    }
}
