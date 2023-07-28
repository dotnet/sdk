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

        private static string[] GetNamedArguments(string separator) => new string[] {
            "",
            $", startIndex{separator}1",
            $", startIndex{separator}1, count{separator}1",
            $", count{separator}1, startIndex{separator}1"
        };

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

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedNamedData() => DiagnosedAndFixedNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedNamedData() => DiagnosedAndFixedNamedData(VisualBasicSeparator);
        private static IEnumerable<object[]> DiagnosedAndFixedNamedData(string separator)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"a.{caseChanging}().{method}(value{separator}b)", $"a.{method}(value{separator}b, comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in GetNamedArguments(separator))
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{arguments})", $"a.IndexOf(value{separator}b{arguments}, comparisonType{separator}StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> DiagnosedAndFixedWithEqualsToData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    // Tests implicit boolean check
                    yield return new object[] { $"a.{caseChanging}().{method}(b)", $"a.{method}(b, StringComparison.{replacement})", "" };
                }

                // Tests having an appended method invocation at the end

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(b{arguments})", $"a.IndexOf(b{arguments}, StringComparison.{replacement})", ".Equals(-1)" };
                }
            }
        }

        public static IEnumerable<object[]> DiagnosedAndFixedWithEqualsToInvertedData()
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    // Tests implicit boolean check
                    yield return new object[] { $"a.{method}(b.{caseChanging}())", $"a.{method}(b, StringComparison.{replacement})", "" };
                }

                // Tests having an appended method invocation at the end

                // IndexOf overloads
                foreach (string arguments in UnnamedArgs)
                {
                    yield return new object[] { $"a.IndexOf(b.{caseChanging}(){arguments})", $"a.IndexOf(b{arguments}, StringComparison.{replacement})", ".Equals(-1)" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedWithEqualsToNamedData() => DiagnosedAndFixedWithEqualsToNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedWithEqualsToNamedData() => DiagnosedAndFixedWithEqualsToNamedData(VisualBasicSeparator);
        private static IEnumerable<object[]> DiagnosedAndFixedWithEqualsToNamedData(string separator)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    // Tests implicit boolean check
                    yield return new object[] { $"a.{caseChanging}().{method}(value{separator}b)", $"a.{method}(value{separator}b, comparisonType{separator}StringComparison.{replacement})", "" };
                }

                // Tests having an appended method invocation at the end

                // IndexOf overloads
                foreach (string arguments in GetNamedArguments(separator))
                {
                    yield return new object[] { $"a.{caseChanging}().IndexOf(value{separator}b{arguments})", $"a.IndexOf(value{separator}b{arguments}, comparisonType{separator}StringComparison.{replacement})", ".Equals(-1)" };
                }
            }
        }

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

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedStringLiteralsNamedData() => DiagnosedAndFixedStringLiteralsNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedStringLiteralsNamedData() => DiagnosedAndFixedStringLiteralsNamedData(VisualBasicSeparator);
        private static IEnumerable<object[]> DiagnosedAndFixedStringLiteralsNamedData(string separator)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"\"aBc\".{caseChanging}().{method}(value{separator}\"CdE\")", $"\"aBc\".{method}(value{separator}\"CdE\", comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in GetNamedArguments(separator))
                {
                    yield return new object[] { $"\"aBc\".{caseChanging}().IndexOf(value{separator}\"CdE\"{arguments})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{arguments}, comparisonType{separator}StringComparison.{replacement})" };
                }
            }
        }

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

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedStringReturningMethodsNamedData() => DiagnosedAndFixedStringReturningMethodsNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedStringReturningMethodsNamedData() => DiagnosedAndFixedStringReturningMethodsNamedData(VisualBasicSeparator);
        private static IEnumerable<object[]> DiagnosedAndFixedStringReturningMethodsNamedData(string separator)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"GetStringA().{caseChanging}().{method}(value{separator}GetStringB())", $"GetStringA().{method}(value{separator}GetStringB(), comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in GetNamedArguments(separator))
                {
                    yield return new object[] { $"GetStringA().{caseChanging}().IndexOf(value{separator}GetStringB(){arguments})", $"GetStringA().IndexOf(value{separator}GetStringB(){arguments}, comparisonType{separator}StringComparison.{replacement})" };
                }
            }
        }

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

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedParenthesizedNamedData() => DiagnosedAndFixedParenthesizedNamedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedParenthesizedNamedData() => DiagnosedAndFixedParenthesizedNamedData(VisualBasicSeparator);
        private static IEnumerable<object[]> DiagnosedAndFixedParenthesizedNamedData(string separator)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"(\"aBc\".{caseChanging}()).{method}(value{separator}\"CdE\")", $"\"aBc\".{method}(value{separator}\"CdE\", comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in GetNamedArguments(separator))
                {
                    yield return new object[] { $"(\"aBc\".{caseChanging}()).IndexOf(value{separator}\"CdE\"{arguments})", $"\"aBc\".IndexOf(value{separator}\"CdE\"{arguments}, comparisonType{separator}StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedParenthesizedNamedInvertedData() => DiagnosedAndFixedParenthesizedNamedInvertedData(CSharpSeparator);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedParenthesizedNamedInvertedData() => DiagnosedAndFixedParenthesizedNamedInvertedData(VisualBasicSeparator);
        private static IEnumerable<object[]> DiagnosedAndFixedParenthesizedNamedInvertedData(string separator)
        {
            foreach ((string caseChanging, string replacement) in Cultures)
            {
                foreach (string method in ContainsStartsWith)
                {
                    yield return new object[] { $"(GetString()).{method}(value{separator}(GetString().{caseChanging}()))", $"(GetString()).{method}(value{separator}GetString(), comparisonType{separator}StringComparison.{replacement})" };
                }

                // IndexOf overloads
                foreach (string arguments in GetNamedArguments(separator))
                {
                    yield return new object[] { $"(GetString()).IndexOf(value{separator}(GetString().{caseChanging}()){arguments})", $"(GetString()).IndexOf(value{separator}GetString(){arguments}, comparisonType{separator}StringComparison.{replacement})" };
                }
            }
        }

        public static IEnumerable<object[]> CSharpDiagnosedAndFixedEqualityToEqualsData() =>
            DiagnosedAndFixedEqualityToEqualsData(CSharpComparisonOperators);
        public static IEnumerable<object[]> VisualBasicDiagnosedAndFixedEqualityToEqualsData() =>
            DiagnosedAndFixedEqualityToEqualsData(VisualBasicComparisonOperators);
        private static IEnumerable<object[]> DiagnosedAndFixedEqualityToEqualsData(ValueTuple<string, string>[] comparisonOperators)
        {
#pragma warning disable format
            foreach (string casing in new[]{ "Lower", "Upper" })
            {
                foreach ((string before, string after) in comparisonOperators)
                {
                    yield return new object[] { $"a.To{casing}() {before} b.To{casing}()",          $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}() {before} b.To{casing}Invariant()", $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}() {before} \"abc\"",              $"{after}a.Equals(\"abc\", StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}() {before} b",                    $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };

                    yield return new object[] { $"a.To{casing}Invariant() {before} b.To{casing}Invariant()", $"{after}a.Equals(b, StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}Invariant() {before} b.To{casing}()",          $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}Invariant() {before} \"abc\"",              $"{after}a.Equals(\"abc\", StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"a.To{casing}Invariant() {before} b",                    $"{after}a.Equals(b, StringComparison.InvariantCultureIgnoreCase)" };

                    yield return new object[] { $"a {before} b.To{casing}()",          $"{after}a.Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"a {before} b.To{casing}Invariant()", $"{after}a.Equals(b, StringComparison.InvariantCultureIgnoreCase)" };

                    yield return new object[] { $"\"abc\" {before} b.To{casing}()",                                $"{after}\"abc\".Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\" {before} b.To{casing}Invariant()",                       $"{after}\"abc\".Equals(b, StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\".To{casing}() {before} b.To{casing}()",                   $"{after}\"abc\".Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\".To{casing}() {before} b.To{casing}Invariant()",          $"{after}\"abc\".Equals(b, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"\"abc\".To{casing}Invariant() {before} b.To{casing}Invariant()", $"{after}\"abc\".Equals(b, StringComparison.InvariantCultureIgnoreCase)" };

                    yield return new object[] { $"GetString().To{casing}() {before} a.To{casing}()",          $"{after}GetString().Equals(a, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}() {before} a.To{casing}Invariant()", $"{after}GetString().Equals(a, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}Invariant() {before} a.To{casing}()", $"{after}GetString().Equals(a, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}() {before} a",                       $"{after}GetString().Equals(a, StringComparison.CurrentCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}Invariant() {before} a",              $"{after}GetString().Equals(a, StringComparison.InvariantCultureIgnoreCase)" };
                    yield return new object[] { $"GetString().To{casing}Invariant() {before} \"abc\"",        $"{after}GetString().Equals(\"abc\", StringComparison.InvariantCultureIgnoreCase)" };
                }
            }
#pragma warning restore format
        }

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

        public static IEnumerable<object[]> CSharpDiagnosticNoFixEqualsData() => DiagnosticNoFixEqualsData(CSharpComparisonOperators);
        public static IEnumerable<object[]> VisualBasicDiagnosticNoFixEqualsData() => DiagnosticNoFixEqualsData(VisualBasicComparisonOperators);

        private static IEnumerable<object[]> DiagnosticNoFixEqualsData(ValueTuple<string, string>[] ops)
        {
            foreach ((string op, _) in ops)
            {
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
        private static IEnumerable<object[]> DiagnosticNoFixCompareToNamedData(string separator)
        {
            // Tests need to define strings a, b
            foreach ((string caseChanging, _) in Cultures)
            {
                yield return new object[] { $"a.{caseChanging}().CompareTo(strB{separator}b)" };
                yield return new object[] { $"(\"aBc\".{caseChanging}()).CompareTo(strB{separator}\"CdE\")" };

                // Inverted
                yield return new object[] { $"a.CompareTo(strB{separator}b.{caseChanging}())" };
                yield return new object[] { $"(\"aBc\").CompareTo(strB{separator}\"CdE\".{caseChanging}())" };
            }
        }
    }
}