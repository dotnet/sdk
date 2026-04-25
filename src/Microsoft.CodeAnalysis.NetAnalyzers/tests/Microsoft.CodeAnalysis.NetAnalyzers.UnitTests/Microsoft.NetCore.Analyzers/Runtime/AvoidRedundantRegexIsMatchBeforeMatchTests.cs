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
        public async Task StaticWithOptionsAndTimeout_Flags()
        {
            // Timeout overload: all four arguments (input, pattern, options, timeout) must be preserved.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var timeout = TimeSpan.FromSeconds(1);
                        if ({|CA2028:Regex.IsMatch(input, @"\d+", RegexOptions.IgnoreCase, timeout)|})
                        {
                            var m = Regex.Match(input, @"\d+", RegexOptions.IgnoreCase, timeout);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var timeout = TimeSpan.FromSeconds(1);
                        if (Regex.Match(input, @"\d+", RegexOptions.IgnoreCase, timeout) is { Success: true } m)
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
            // Real-world pattern: regex.Match(x).Groups[1].Value
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
            await VerifyCodeFixCSharp9Async(source, source);
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
            await VerifyCodeFixCSharp9Async(source, source);
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
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.Match(input, "hello") is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
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
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        var regex = new Regex(@"\d+");
                        if (regex.Match(input, 0) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
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
            // Real-world false positive: different IsMatch and Match patterns on same input.
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

        [Fact]
        public async Task ReadonlyFieldReceiverReassigned_NoDiagnostic()
        {
            // Receiver is local.ReadonlyField — reassigning the local between IsMatch
            // and Match means a different field instance, so not redundant.
            var source = """
                using System.Text.RegularExpressions;

                class Holder
                {
                    public readonly Regex Re = new Regex(@"\d+");
                }

                class C
                {
                    Holder GetA() => new Holder();
                    Holder GetB() => new Holder();

                    void M(string input)
                    {
                        Holder h = GetA();
                        if (h.Re.IsMatch(input))
                        {
                            h = GetB();
                            Match m = h.Re.Match(input);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task ReadonlyFieldArgumentReassigned_NoDiagnostic()
        {
            // Argument is local.ReadonlyField — reassigning the local between
            // IsMatch and Match means a different field value, so not redundant.
            var source = """
                using System.Text.RegularExpressions;

                class Holder
                {
                    public readonly string Pattern = @"\d+";
                }

                class C
                {
                    Holder GetA() => new Holder();
                    Holder GetB() => new Holder();

                    void M(string input)
                    {
                        Holder h = GetA();
                        if (Regex.IsMatch(input, h.Pattern))
                        {
                            h = GetB();
                            Match m = Regex.Match(input, h.Pattern);
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task SpanOverloadIsMatch_NoDiagnostic()
        {
            // ReadOnlySpan<char> overloads of IsMatch have no corresponding
            // Match overload returning Match — ParameterTypesMatch rejects.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        ReadOnlySpan<char> span = input.AsSpan();
                        if (Regex.IsMatch(span, @"\d+"))
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await new VerifyCS.Test
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net70,
            }.RunAsync(TestContext.Current.CancellationToken);
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
            // Verify diagnostic fires but fixer does not change the code.
            await VerifyCodeFixCSharp9Async(source, source);
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
            // Verify diagnostic fires but fixer does not change the code.
            await VerifyCodeFixCSharp9Async(source, source);
        }

        #endregion

        #region Real-world pattern variants

        [Fact]
        public async Task RealWorld_MatchUsedForGroupsInline()
        {
            // Real-world pattern: inline Groups access on Match result.
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
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task RealWorld_MultiplePairsWithDifferentPatterns_NoDiagnostic()
        {
            // Real-world pattern: multiple IsMatch/Match pairs but with different patterns.
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

        [Fact]
        public async Task RealWorld_ElseIfChainWithIsMatchThenMatch_Flags()
        {
            // Real-world pattern: else-if chain where each branch
            // does IsMatch then Match with the same pattern.
            // Uses distinct variable names so "fix all" doesn't hit C# pattern
            // variable scoping conflicts.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    const string PATTERN_A = @"^\d+\.\d+$";
                    const string PATTERN_B = @"^\d+$";

                    void M(string text)
                    {
                        if ({|CA2028:Regex.IsMatch(text, PATTERN_A)|})
                        {
                            Match ma = Regex.Match(text, PATTERN_A);
                            Console.WriteLine(ma.Value);
                        }
                        else if ({|CA2028:Regex.IsMatch(text, PATTERN_B)|})
                        {
                            Match mb = Regex.Match(text, PATTERN_B);
                            Console.WriteLine(mb.Value);
                        }
                    }
                }
                """;
            var fixedSource = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    const string PATTERN_A = @"^\d+\.\d+$";
                    const string PATTERN_B = @"^\d+$";

                    void M(string text)
                    {
                        if (Regex.Match(text, PATTERN_A) is { Success: true } ma)
                        {
                            Console.WriteLine(ma.Value);
                        }
                        else if (Regex.Match(text, PATTERN_B) is { Success: true } mb)
                        {
                            Console.WriteLine(mb.Value);
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task RealWorld_IsMatchThenMatchInsideLoop_Flags()
        {
            // Real-world pattern: IsMatch/Match pair inside a foreach loop body.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string[] args)
                    {
                        foreach (string arg in args)
                        {
                            if ({|CA2028:Regex.IsMatch(arg, @"^-config=(.+)$")|})
                            {
                                Match m = Regex.Match(arg, @"^-config=(.+)$");
                                Console.WriteLine(m.Groups[1].Value);
                            }
                        }
                    }
                }
                """;
            var fixedSource = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string[] args)
                    {
                        foreach (string arg in args)
                        {
                            if (Regex.Match(arg, @"^-config=(.+)$") is { Success: true } m)
                            {
                                Console.WriteLine(m.Groups[1].Value);
                            }
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task RealWorld_ConstStringFieldPattern_Flags()
        {
            // Real-world pattern: const string fields used as regex pattern arg.
            var source = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    const string COMPONENT_PATTERN = @"(\w+)\((\w+)\)";

                    void M(string text)
                    {
                        if ({|CA2028:Regex.IsMatch(text, COMPONENT_PATTERN)|})
                        {
                            Match m = Regex.Match(text, COMPONENT_PATTERN);
                            string name = m.Groups[1].Value;
                            string type = m.Groups[2].Value;
                        }
                    }
                }
                """;
            var fixedSource = """
                using System;
                using System.Text.RegularExpressions;

                class C
                {
                    const string COMPONENT_PATTERN = @"(\w+)\((\w+)\)";

                    void M(string text)
                    {
                        if (Regex.Match(text, COMPONENT_PATTERN) is { Success: true } m)
                        {
                            string name = m.Groups[1].Value;
                            string type = m.Groups[2].Value;
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
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
            // Fixer removes only the first Match declaration.
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
            var fixedSource = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (Regex.Match(input, @"\d+") is { Success: true } m)
                        {
                            Match m2 = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
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
            await VerifyCodeFixCSharp9Async(source, source);
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
            await VerifyCodeFixCSharp9Async(source, source);
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

        [Fact]
        public async Task NamedArgumentsSameOrder_Diagnostic()
        {
            // Named arguments in same order as parameters — should still match.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input: input, pattern: @"\d+")|})
                        {
                            Match m = Regex.Match(input: input, pattern: @"\d+");
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
                        if (Regex.Match(input: input, pattern: @"\d+") is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task NamedArgumentsReordered_Diagnostic()
        {
            // Named arguments reordered between IsMatch and Match — same values, different order.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input: input, pattern: @"\d+")|})
                        {
                            Match m = Regex.Match(pattern: @"\d+", input: input);
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
                        if (Regex.Match(pattern: @"\d+", input: input) is { Success: true } m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task NamedArgumentsDifferentValues_NoDiagnostic()
        {
            // Named arguments with same names but different values — should NOT match.
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, string input2)
                    {
                        if (Regex.IsMatch(input: input, pattern: @"\d+"))
                        {
                            Match m = Regex.Match(input: input2, pattern: @"\d+");
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        #endregion

        #region Regression tests for mutation and control-flow edge cases

        [Fact]
        public async Task InterveningRefMutation_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void Normalize(ref string s) { s = s.Trim(); }

                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            Normalize(ref input);
                            var m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task InterveningOutMutation_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    bool TryNormalize(string s, out string result) { result = s.Trim(); return true; }

                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            TryNormalize(input, out input);
                            var m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task NameConflictAfterIfStatement_DiagnosticButNoFix()
        {
            // The conflicting 'int m' must be in a sibling block so the original
            // code compiles (sibling scopes may reuse names). After the fixer
            // transforms the condition to 'is { } m', the pattern variable scopes
            // to the entire enclosing block, colliding with the later 'm'.
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
                        {
                            int m = 0; // sibling block — valid now, but would conflict with pattern var
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task NameConflictWithPatternInElse_DiagnosticButNoFix()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, object obj)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                        else if (obj is int m)
                        {
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task NameConflictWithForeachInElse_DiagnosticButNoFix()
        {
            var source = """
                using System.Text.RegularExpressions;
                using System.Collections.Generic;

                class C
                {
                    void M(string input, List<int> items)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                        else
                        {
                            foreach (var m in items) { }
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task DeclaredAsGroupType_DiagnosticButNoFix()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Group g = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task DeclaredAsObjectType_DiagnosticButNoFix()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            object o = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task DeclaredAsExactMatchType_FixOffered()
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
        public async Task ParenthesizedCondition_Diagnostic()
        {
            // Parenthesized IsMatch condition: if ((Regex.IsMatch(input, pattern)))
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        if (({|CA2028:Regex.IsMatch(input, @"\d+")|})  )
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
        public async Task DeconstructionForeachNameCollision_ElseBranch_DiagnosticNoFix()
        {
            // Deconstruction foreach in else branch with conflicting variable name
            var source = """
                using System.Text.RegularExpressions;
                using System.Collections.Generic;

                class C
                {
                    void M(string input, List<(string, int)> items)
                    {
                        if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                        {
                            Match m = Regex.Match(input, @"\d+");
                        }
                        else
                        {
                            foreach (var (m, count) in items) { }
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task NonBlockParent_DiagnosticNoFix()
        {
            // If statement whose parent is not a block (e.g., switch section)
            // — fixer conservatively doesn't offer fix
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input, int mode)
                    {
                        switch (mode)
                        {
                            case 1:
                                if ({|CA2028:Regex.IsMatch(input, @"\d+")|})
                                {
                                    Match m = Regex.Match(input, @"\d+");
                                }
                                break;
                        }
                    }
                }
                """;
            await VerifyCodeFixCSharp9Async(source, source);
        }

        #endregion

        #region Coalesce and deconstruction write tests

        [Fact]
        public async Task InterveningCoalesceAssignment_NoDiagnostic()
        {
            var source = """
                #nullable enable
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string? input)
                    {
                        if (Regex.IsMatch(input!, @"\d+"))
                        {
                            input ??= "default";
                            var m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            // ??= and nullable require C# 8+
            await new VerifyCS.Test
            {
                TestCode = source,
                LanguageVersion = LanguageVersion.CSharp9,
            }.RunAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task InterveningDeconstructionAssignment_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    (string, string) Split(string s) => (s, s);

                    void M(string input)
                    {
                        if (Regex.IsMatch(input, @"\d+"))
                        {
                            (input, _) = Split(input);
                            var m = Regex.Match(input, @"\d+");
                        }
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        #endregion

        #region Non-block write and lambda parameter conflict tests

        [Fact]
        public async Task SingleStatementBody_WriteToTrackedSymbol_NoDiagnostic()
        {
            var source = """
                using System.Text.RegularExpressions;

                class C
                {
                    void M(string input)
                    {
                        string result = "";
                        if (Regex.IsMatch(input, @"\d+"))
                            result = Regex.Match(input = "other", @"\d+").Value;
                    }
                }
                """;
            await VerifyCS.VerifyAnalyzerAsync(source);
        }

        [Fact]
        public async Task LambdaParameterConflictsWithMatchVariable_NoFix()
        {
            // Fixer should not offer fix because 'm' is used as a lambda parameter in else branch
            var source = """
                using System;
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
                            Func<int, int> f = m => m + 1;
                        }
                    }
                }
                """;
            // Fix not applicable — lambda parameter 'm' conflicts
            await VerifyCodeFixCSharp9Async(source, source);
        }

        [Fact]
        public async Task LocalFunctionParameterConflictsWithMatchVariable_NoFix()
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
                        else
                        {
                            int F(int m) => m + 1;
                            _ = F(0);
                        }
                    }
                }
                """;
            // Fix not applicable — local function parameter 'm' conflicts
            await VerifyCodeFixCSharp9Async(source, source);
        }

        #endregion
    }
}
