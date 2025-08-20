// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.NetCore.Analyzers.Performance.UnitTests
{
    public abstract class RecommendCaseInsensitiveStringComparison_Base_Tests
    {
        private static readonly (string, string)[] Cultures = new[] {
            ("ToLower", "CurrentCultureIgnoreCase"),
            ("ToUpper", "CurrentCultureIgnoreCase"),
            ("ToLowerInvariant", "InvariantCultureIgnoreCase"),
            ("ToUpperInvariant", "InvariantCultureIgnoreCase")
        };

        private static readonly (string, string)[] IncompatibleCaseChangingMethods = new[]
        {
            ("ToLower", "ToLowerInvariant"),
            ("ToLower", "ToUpperInvariant"),
            ("ToUpper", "ToLowerInvariant"),
            ("ToUpper", "ToUpperInvariant"),
            ("ToLowerInvariant", "ToLower"),
            ("ToLowerInvariant", "ToUpper"),
            ("ToUpperInvariant", "ToLower"),
            ("ToUpperInvariant", "ToUpper"),
        };

        private static readonly string[] ContainsStartsWith = new[] { "Contains", "StartsWith" };
        private static readonly string[] UnnamedArgs = new[] { "", ", 1", ", 1, 1" };

        private static readonly (string, string)[] CSharpComparisonOperators = new[] {
            ("==", ""),
            ("!=", "!")
        };
        private static readonly (string, string)[] VisualBasicComparisonOperators = new[] {
            ("=", ""),
            ("<>", "Not ")
        };

        private const string CSharpSeparator = ": ";
        private const string VisualBasicSeparator = ":=";

        private static (string, string)[] GetNamedArguments(string separator) => new (string, string)[] {
            ("", ""),
            ($", startIndex{separator}1", $", startIndex{separator}1"),
            ($", startIndex{separator}1, count{separator}1", $", startIndex{separator}1, count{separator}1")
        };

        // In C#, when the user names the arguments and passes them in a different order than the original, the fixer will
        // be able to get the arguments from IInvocationOperation.Arguments in the order the user passed.
        private static (string, string) CSharpIndexOfNamedArguments = new($", count: 1, startIndex: 1", $", count: 1, startIndex: 1");

        // In VB, when the user names the arguments and passes them in a different order than the original, the fixer will
        // not be able to get the arguments from IINvocation.Arguments in the order the user passed, and instead will
        // get them in the original order as defined in the method signature.
        private static (string, string) VisualBasicIndexOfNamedArguments = new($", count:=1, startIndex:=1", $", startIndex:=1, count:=1");

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is case-changed and the string argument is passed unmodified.
        public static IEnumerable<object[]> DiagnosedAndFixedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{caseChanging}().{method}(b)", $"a.{method}(b, StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(b{arguments})", $"a.IndexOf(b{arguments}, StringComparison.{replacement})" };
                }
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is unmodified and the string argument is passed case-changed.
        public static IEnumerable<object[]> DiagnosedAndFixedInvertedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{method}(b.{caseChanging}())", $"a.{method}(b, StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.IndexOf(b.{caseChanging}(){arguments})", $"a.IndexOf(b{arguments}, StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedNamedData() => DiagnosedAndFixedNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedNamedData() => DiagnosedAndFixedNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is case-changed and the string argument is passed unmodified and explicitly named.
        private static IEnumerable<object[]> DiagnosedAndFixedNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{caseChanging}().{method}(value{separator}b)", $"a.{method}(value{separator}b, comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{originalArguments})", $"a.IndexOf(value{separator}b{expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item1})", $"a.IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedInvertedNamedData() => DiagnosedAndFixedInvertedNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedInvertedNamedData() => DiagnosedAndFixedInvertedNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is unmodified and the string argument is passed case-changed and explicitly named.
        private static IEnumerable<object[]> DiagnosedAndFixedInvertedNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{method}(value{separator}b.{caseChanging}())", $"a.{method}(value{separator}b, comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"a.IndexOf(value{separator}b.{caseChanging}(){originalArguments})", $"a.IndexOf(value{separator}b{expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item1})", $"a.IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is case-changed and the string argument is unmodified
        // and the result of the method then calls another method.
        public static IEnumerable<object[]> DiagnosedAndFixedWithAppendedMethodData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{caseChanging}().{method}(b)", $"a.{method}(b, StringComparison.{replacement})", ".Equals(myBoolean)" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(b{arguments})", $"a.IndexOf(b{arguments}, StringComparison.{replacement})", ".Equals(-1)" };
                }
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is unmodified and the string argument is case-changed
        // and the result of the method then calls another method.
        public static IEnumerable<object[]> DiagnosedAndFixedWithAppendedMethodInvertedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{method}(b.{caseChanging}())", $"a.{method}(b, StringComparison.{replacement})", ".Equals(myBoolean)" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.IndexOf(b.{caseChanging}(){arguments})", $"a.IndexOf(b{arguments}, StringComparison.{replacement})", ".Equals(-1)" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedWithAppendedMethodNamedData() => DiagnosedAndFixedWithAppendedMethodNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedWithAppendedMethodNamedData() => DiagnosedAndFixedWithAppendedMethodNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is case-changed and the string argument is unmodified and explicitly named
        // and the result of the method then calls another method.
        private static IEnumerable<object[]> DiagnosedAndFixedWithAppendedMethodNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{caseChanging}().{method}(value{separator}b)", $"a.{method}(value{separator}b, comparisonType{separator}StringComparison.{replacement})", ".Equals(myBoolean)" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{originalArguments})", $"a.IndexOf(value{separator}b{expectedArguments}, comparisonType{separator}StringComparison.{replacement})", ".Equals(-1)" };
                }

                yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item1})", $"a.IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})", ".Equals(-1)" };
            }
        }

        // Test cases for Contains, StartsWith
        // where the instance string is case-changed and the string argument is unmodified
        // and the boolean result is consumed in a condition statement.
        public static IEnumerable<object[]> DiagnosedAndFixedImplicitBooleanData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{caseChanging}().{method}(b)", $"a.{method}(b, StringComparison.{replacement})", "" };
                }

                // Inverted
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{method}(b.{caseChanging}())", $"a.{method}(b, StringComparison.{replacement})", "" };
                }
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is a case-changed literal and the string argument is an unmodified string literal.
        public static IEnumerable<object[]> DiagnosedAndFixedStringLiteralsData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"\"aBc\".{caseChanging}().{method}(\"CdE\")", $"\"aBc\".{method}(\"CdE\", StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"\"aBc\".{caseChanging}().IndexOf(\"CdE\"{arguments})", $"\"aBc\".IndexOf(\"CdE\"{arguments}, StringComparison.{replacement})" };
                }
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is an unmodified literal and the string argument is a case-changed string literal.
        public static IEnumerable<object[]> DiagnosedAndFixedStringLiteralsInvertedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"\"aBc\".{method}(\"CdE\".{caseChanging}())", $"\"aBc\".{method}(\"CdE\", StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"\"aBc\".IndexOf(\"CdE\".{caseChanging}(){arguments})", $"\"aBc\".IndexOf(\"CdE\"{arguments}, StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedStringLiteralsNamedData() => DiagnosedAndFixedStringLiteralsNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedStringLiteralsNamedData() => DiagnosedAndFixedStringLiteralsNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is a case-changed literal and the string argument is an unmodified, explicitly named string literal.
        private static IEnumerable<object[]> DiagnosedAndFixedStringLiteralsNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"\"aBc\".{caseChanging}().{method}(value{separator}\"CdE\")", $"\"aBc\".{method}(value{separator}\"CdE\", comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"\"aBc\".{caseChanging}().IndexOf(value{separator}\"CdE\"{originalArguments})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"\"aBc\".{caseChanging}().IndexOf(value{separator}\"CdE\"{langSpecificOrderedNamedArgs.Item1})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedStringLiteralsInvertedNamedData() => DiagnosedAndFixedStringLiteralsInvertedNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedStringLiteralsInvertedNamedData() => DiagnosedAndFixedStringLiteralsInvertedNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is an unmodified literal and the string argument is a case-changed, explicitly named string literal.
        private static IEnumerable<object[]> DiagnosedAndFixedStringLiteralsInvertedNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"\"aBc\".{method}(value{separator}\"CdE\".{caseChanging}())", $"\"aBc\".{method}(value{separator}\"CdE\", comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"\"aBc\".IndexOf(value{separator}\"CdE\".{caseChanging}(){originalArguments})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"\"aBc\".{caseChanging}().IndexOf(value{separator}\"CdE\"{langSpecificOrderedNamedArgs.Item1})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is a method invocation that is case-changed, and the string argument is unmodified and comes from another method invocation.
        public static IEnumerable<object[]> DiagnosedAndFixedStringReturningMethodsData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"GetStringA().{caseChanging}().{method}(GetStringB())", $"GetStringA().{method}(GetStringB(), StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"GetStringA().{caseChanging}().IndexOf(GetStringB(){arguments})", $"GetStringA().IndexOf(GetStringB(){arguments}, StringComparison.{replacement})" };
                }
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is a method invocation and is unmodified, and the string argument is case-changed and comes from another method invocation.
        public static IEnumerable<object[]> DiagnosedAndFixedStringReturningMethodsInvertedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"GetStringA().{method}(GetStringB().{caseChanging}())", $"GetStringA().{method}(GetStringB(), StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"GetStringA().IndexOf(GetStringB().{caseChanging}(){arguments})", $"GetStringA().IndexOf(GetStringB(){arguments}, StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedStringReturningMethodsNamedData() => DiagnosedAndFixedStringReturningMethodsNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedStringReturningMethodsNamedData() => DiagnosedAndFixedStringReturningMethodsNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is a method invocation that is case-changed, and the string argument is unmodified, comes from another method invocation, and is explicitly named.
        private static IEnumerable<object[]> DiagnosedAndFixedStringReturningMethodsNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"GetStringA().{caseChanging}().{method}(value{separator}GetStringB())", $"GetStringA().{method}(value{separator}GetStringB(), comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"GetStringA().{caseChanging}().IndexOf(value{separator}GetStringB(){originalArguments})", $"GetStringA().IndexOf(value{separator}GetStringB(){expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"GetStringA().{caseChanging}().IndexOf(value{separator}GetStringB(){langSpecificOrderedNamedArgs.Item1})", $"GetStringA().IndexOf(value{separator}GetStringB(){langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedStringReturningMethodsInvertedNamedData() => DiagnosedAndFixedStringReturningMethodsInvertedNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedStringReturningMethodsInvertedNamedData() => DiagnosedAndFixedStringReturningMethodsInvertedNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the instance string is unmodified and comes from a method invocation, and the string argument is case-changed, comes from another method invocation, and is explicitly named.
        private static IEnumerable<object[]> DiagnosedAndFixedStringReturningMethodsInvertedNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"GetStringA().{method}(value{separator}GetStringB().{caseChanging}())", $"GetStringA().{method}(value{separator}GetStringB(), comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"GetStringA().IndexOf(value{separator}GetStringB().{caseChanging}(){originalArguments})", $"GetStringA().IndexOf(value{separator}GetStringB(){expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"GetStringA().{caseChanging}().IndexOf(value{separator}GetStringB(){langSpecificOrderedNamedArgs.Item1})", $"GetStringA().IndexOf(value{separator}GetStringB(){langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the case-changed invocation string literal is enclosed in parenthesis, and the argument is another string literal.
        public static IEnumerable<object[]> DiagnosedAndFixedParenthesizedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"(\"aBc\".{caseChanging}()).{method}(\"CdE\")", $"\"aBc\".{method}(\"CdE\", StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"(\"aBc\".{caseChanging}()).IndexOf(\"CdE\"{arguments})", $"\"aBc\".IndexOf(\"CdE\"{arguments}, StringComparison.{replacement})" };
                }
            }
        }

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the invocation is a string literal and the argument is a case-changed string literal enclosed in parenthesis.
        public static IEnumerable<object[]> DiagnosedAndFixedParenthesizedInvertedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"\"aBc\".{method}((\"CdE\".{caseChanging}()))", $"\"aBc\".{method}(\"CdE\", StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"\"aBc\".IndexOf(\"CdE\".{caseChanging}(){arguments})", $"\"aBc\".IndexOf(\"CdE\"{arguments}, StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedParenthesizedNamedData() => DiagnosedAndFixedParenthesizedNamedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedParenthesizedNamedData() => DiagnosedAndFixedParenthesizedNamedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the case-changed invocation string literal is enclosed in parenthesis, and the argument is another string literal, explicitly named.
        private static IEnumerable<object[]> DiagnosedAndFixedParenthesizedNamedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"(\"aBc\".{caseChanging}()).{method}(value{separator}\"CdE\")", $"\"aBc\".{method}(value{separator}\"CdE\", comparisonType{separator}StringComparison.{replacement})" };
                    yield return new object[] { $"(GetString().{caseChanging}()).{method}(value{separator}GetString())", $"GetString().{method}(value{separator}GetString(), comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"(\"aBc\".{caseChanging}()).IndexOf(value{separator}\"CdE\"{originalArguments})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                    yield return new object[] { $"(GetString().{caseChanging}()).IndexOf(value{separator}GetString(){originalArguments})", $"GetString().IndexOf(value{separator}GetString(){expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item1})", $"a.IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedParenthesizedNamedInvertedData() => DiagnosedAndFixedParenthesizedNamedInvertedData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedParenthesizedNamedInvertedData() => DiagnosedAndFixedParenthesizedNamedInvertedData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the invocation is a string literal and the argument is a case-changed string literal enclosed in parenthesis and explicitly named.
        private static IEnumerable<object[]> DiagnosedAndFixedParenthesizedNamedInvertedData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"(\"aBc\").{method}(value{separator}(\"cDe\".{caseChanging}()))", $"(\"aBc\").{method}(value{separator}\"cDe\", comparisonType{separator}StringComparison.{replacement})" };
                    yield return new object[] { $"(GetString()).{method}(value{separator}(GetString().{caseChanging}()))", $"(GetString()).{method}(value{separator}GetString(), comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"(\"aBc\").IndexOf(value{separator}(\"cDe\".{caseChanging}()){originalArguments})", $"(\"aBc\").IndexOf(value{separator}\"cDe\"{expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                    yield return new object[] { $"(GetString()).IndexOf(value{separator}(GetString().{caseChanging}()){originalArguments})", $"(GetString()).IndexOf(value{separator}GetString(){expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item1})", $"a.IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedParenthesizedComplexCasesData() => DiagnosedAndFixedParenthesizedComplexCasesData(CSharpSeparator, CSharpIndexOfNamedArguments);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedParenthesizedComplexCasesData() => DiagnosedAndFixedParenthesizedComplexCasesData(VisualBasicSeparator, VisualBasicIndexOfNamedArguments);

        // Test cases for Contains, StartsWith and all overloads of IndexOf
        // where the invocation and the argument are both enclosed in various combinations of parenthesis.
        private static IEnumerable<object[]> DiagnosedAndFixedParenthesizedComplexCasesData(string separator, (string, string) langSpecificOrderedNamedArgs)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"((GetString().{caseChanging}())).{method}(value{separator}((GetString().{caseChanging}())))", $"GetString().{method}(value{separator}GetString(), comparisonType{separator}StringComparison.{replacement})" };
                    yield return new object[] { $"((a.{caseChanging}())).{method}(value{separator}((b.{caseChanging}())))", $"a.{method}(value{separator}b, comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach ((string originalArguments, string expectedArguments) in GetNamedArguments(separator))
                {
                    yield return new object[] { $"((GetString().{caseChanging}())).IndexOf(value{separator}((GetString().{caseChanging}())){originalArguments})", $"GetString().IndexOf(value{separator}GetString(){expectedArguments}, comparisonType{separator}StringComparison.{replacement})" };
                }

                yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item1})", $"a.IndexOf(value{separator}b{langSpecificOrderedNamedArgs.Item2}, comparisonType{separator}StringComparison.{replacement})" };
            }
        }

        // Variety of test cases that should not emit a diagnostic.
        public static IEnumerable<object[]> NoDiagnosticData()
        {
            // Test needs to define a char ch and an object obj
            foreach (string method in new[] { "Contains", "IndexOf", "StartsWith" })
            {
                yield return new object[] { $"\"aBc\".{method}(\"cDe\")" };
                yield return new object[] { $"\"aBc\".{method}(\"cDe\", StringComparison.CurrentCultureIgnoreCase)" };
                yield return new object[] { $"\"aBc\".ToUpper().{method}(\"cDe\", StringComparison.InvariantCulture)" };
                yield return new object[] { $"\"aBc\".{method}(ch)" };

                // Inverted
                yield return new object[] { $"\"aBc\".{method}(\"cDe\".ToUpper(), StringComparison.InvariantCulture)" };
            }

            // StarstWith does not have a (char, StringComparison) overload
            foreach (string method in new[] { "Contains", "IndexOf" })
            {
                yield return new object[] { $"\"aBc\".{method}(ch, StringComparison.Ordinal)" };
                yield return new object[] { $"\"aBc\".ToLowerInvariant().{method}(ch, StringComparison.CurrentCulture)" };
            }

            // CompareTo
            yield return new object[] { "\"aBc\".CompareTo(obj)" };
            yield return new object[] { "\"aBc\".ToLower().CompareTo(obj)" };
            yield return new object[] { "\"aBc\".CompareTo(\"cDe\")" };
        }

        public static IEnumerable<object[]> DiagnosticNoFixStartsWithContainsIndexOfData()
        {
            foreach ((string left, string right) in IncompatibleCaseChangingMethods)
            {
                yield return new object[] { $"a.{left}().StartsWith(b.{right}())" };
                yield return new object[] { $"a.{left}().Contains(b.{right}())" };
                yield return new object[] { $"\"aBc\".{left}().StartsWith(\"cDe\".{right}())" };
                yield return new object[] { $"\"aBc\".{left}().Contains(\"cDe\".{right}())" };
                yield return new object[] { $"GetStringA().{left}().StartsWith(GetStringB().{right}())" };
                yield return new object[] { $"GetStringA().{left}().Contains(GetStringB().{right}())" };

                yield return new object[] { $"a.{left}().StartsWith(b.{right}())" };
                yield return new object[] { $"a.{left}().Contains(b.{right}())" };
                yield return new object[] { $"\"aBc\".{left}().StartsWith(\"cDe\".{right}())" };
                yield return new object[] { $"\"aBc\".{left}().Contains(\"cDe\".{right}())" };

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.{left}().IndexOf(b.{right}(){arguments})" };
                    yield return new object[] { $"a.{left}().IndexOf(b.{right}(){arguments})" };
                }
            }
        }

        // Test cases for CompareTo, with the invocation string case-changed, that should emit a diagnostic but should not offer a fix.
        public static IEnumerable<object[]> DiagnosticNoFixCompareToData()
        {
            // Tests need to define strings a, b, and methods GetStringA, GetStringB
            foreach ((string caseChanging, _) in Cultures)
            {
                yield return new object[] { $"a.{caseChanging}().CompareTo(b)" };
                yield return new object[] { $"\"aBc\".{caseChanging}().CompareTo(\"CdE\")" };
                yield return new object[] { $"GetStringA().{caseChanging}().CompareTo(GetStringB())" };
                yield return new object[] { $"(\"aBc\".{caseChanging}()).CompareTo(\"CdE\")" };
            }
        }

        // Test cases for CompareTo, with the argument string case-changed, that should emit a diagnostic but should not offer a fix.
        public static IEnumerable<object[]> DiagnosticNoFixCompareToInvertedData()
        {
            // Tests need to define strings a, b, and methods GetStringA, GetStringB
            foreach ((string caseChanging, _) in Cultures)
            {
                yield return new object[] { $"a.CompareTo(b.{caseChanging}())" };
                yield return new object[] { $"\"aBc\".CompareTo(\"CdE\".{caseChanging}())" };
                yield return new object[] { $"GetStringA().CompareTo(GetStringB().{caseChanging}())" };
                yield return new object[] { $"(\"aBc\").CompareTo(\"CdE\".{caseChanging}())" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosticNoFixCompareToNamedData() => DiagnosticNoFixCompareToNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosticNoFixCompareToNamedData() => DiagnosticNoFixCompareToNamedData(VisualBasicSeparator);

        // Test cases for CompareTo, with the invocation string case-changed and the argument string explicitly named,
        // that should emit a diagnostic but should not offer a fix.
        private static IEnumerable<object[]> DiagnosticNoFixCompareToNamedData(string separator)
        {
            // Tests need to define strings a, b
            foreach ((string caseChanging, _) in Cultures)
            {
                yield return new object[] { $"a.{caseChanging}().CompareTo(strB{separator}b)" };
                yield return new object[] { $"(\"aBc\".{caseChanging}()).CompareTo(strB{separator}\"CdE\")" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosticNoFixCompareToInvertedNamedData() => DiagnosticNoFixCompareToInvertedNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosticNoFixCompareToInvertedNamedData() => DiagnosticNoFixCompareToInvertedNamedData(VisualBasicSeparator);

        // Test cases for CompareTo, with the invocation string unmodified and the argument string case-changed and explicitly named,
        // that should emit a diagnostic but should not offer a fix.
        private static IEnumerable<object[]> DiagnosticNoFixCompareToInvertedNamedData(string separator)
        {
            // Tests need to define strings a, b
            foreach ((string caseChanging, _) in Cultures)
            {
                yield return new object[] { $"a.CompareTo(strB{separator}b.{caseChanging}())" };
                yield return new object[] { $"(\"aBc\").CompareTo(strB{separator}\"CdE\".{caseChanging}())" };
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedEqualityToEqualsData() =>
            DiagnosedAndFixedEqualityToEqualsData(CSharpComparisonOperators);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedEqualityToEqualsData() =>
            DiagnosedAndFixedEqualityToEqualsData(VisualBasicComparisonOperators);

        // Test cases for string equality binary operations with one side being case-changed,
        // or both sides being case-changed to the same culture.
        private static IEnumerable<object[]> DiagnosedAndFixedEqualityToEqualsData(ValueTuple<string, string>[] comparisonOperators)
        {
#pragma warning disable format
            foreach (string casing in new[]{ "Lower", "Upper" })
            {
                foreach ((string before, string after) in comparisonOperators)
                {
                    yield return new object[] { $"a.To{casing}() {before} b.To{casing}()",          $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}() {before} \"abc\"",              $"{after}a.Equals(\"abc\", StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}() {before} b",                    $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };

                    yield return new object[] { $"a.To{casing}Invariant() {before} b.To{casing}Invariant()", $"{after}a.Equals(b, StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}Invariant() {before} \"abc\"",              $"{after}a.Equals(\"abc\", StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}Invariant() {before} b",                    $"{after}a.Equals(b, StringComparison.InvariantCultureIgnoreCase)" };

                    yield return new object[] { $"a {before} b.To{casing}()",          $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a {before} b.To{casing}Invariant()", $"{after}a.Equals(b, StringComparison.InvariantCultureIgnoreCase)" };

                    yield return new object[] { $"\"abc\" {before} b.To{casing}()",                                $"{after}\"abc\".Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\" {before} b.To{casing}Invariant()",                       $"{after}\"abc\".Equals(b, StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\".To{casing}() {before} b.To{casing}()",                   $"{after}\"abc\".Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\".To{casing}Invariant() {before} b.To{casing}Invariant()", $"{after}\"abc\".Equals(b, StringComparison.InvariantCultureIgnoreCase)" };

                    yield return new object[] { $"GetString().To{casing}() {before} a.To{casing}()",          $"{after}GetString().Equals(a, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}() {before} a",                       $"{after}GetString().Equals(a, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}Invariant() {before} a",              $"{after}GetString().Equals(a, StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}Invariant() {before} \"abc\"",        $"{after}GetString().Equals(\"abc\", StringComparison.InvariantCultureIgnoreCase)" };
                }
            }
#pragma warning restore format
        }

        public static IEnumerable<object[]> CSharpDiagnosticNoFixEqualsData() => DiagnosticNoFixEqualsData(CSharpComparisonOperators);
        public static IEnumerable<object[]> VisualBasicDiagnosticNoFixEqualsData() => DiagnosticNoFixEqualsData(VisualBasicComparisonOperators);

        // Test cases for string equality binary operations that should emit a diagnostic but should not offer a fix
        // because both sides are getting case-changed with different culture.
        private static IEnumerable<object[]> DiagnosticNoFixEqualsData(ValueTuple<string, string>[] comparisonOperators)
        {
            foreach ((string op, _) in comparisonOperators)
            {
                foreach (string casing in new[] { "Lower", "Upper" })
                {
                    yield return new object[] { $"a.To{casing}() {op} b.To{casing}Invariant()" };
                    yield return new object[] { $"\"abc\".To{casing}() {op} b.To{casing}Invariant()" };
                    yield return new object[] { $"GetString().To{casing}() {op} a.To{casing}Invariant()" };

                    yield return new object[] { $"a.To{casing}Invariant() {op} b.To{casing}()" };
                    yield return new object[] { $"\"abc\".To{casing}Invariant() {op} b.To{casing}()" };
                    yield return new object[] { $"GetString().To{casing}Invariant() {op} a.To{casing}()" };
                }

                yield return new object[] { $"\"aBc\".ToLower() {op} \"cDe\".ToLowerInvariant()" };
                yield return new object[] { $"\"aBc\".ToLower() {op} \"cDe\".ToUpperInvariant()" };
                yield return new object[] { $"\"aBc\".ToUpper() {op} \"cDe\".ToLowerInvariant()" };
                yield return new object[] { $"\"aBc\".ToUpper() {op} \"cDe\".ToUpperInvariant()" };

                yield return new object[] { $"\"aBc\".ToLowerInvariant() {op} \"cDe\".ToLower()" };
                yield return new object[] { $"\"aBc\".ToLowerInvariant() {op} \"cDe\".ToUpper()" };
                yield return new object[] { $"\"aBc\".ToUpperInvariant() {op} \"cDe\".ToLower()" };
                yield return new object[] { $"\"aBc\".ToUpperInvariant() {op} \"cDe\".ToUpper()" };

            }
        }
    }
}