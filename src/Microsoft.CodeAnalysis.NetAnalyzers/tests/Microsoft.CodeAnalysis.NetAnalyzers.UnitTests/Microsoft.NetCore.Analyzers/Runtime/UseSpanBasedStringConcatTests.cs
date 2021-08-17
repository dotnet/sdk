// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpUseSpanBasedStringConcat,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpUseSpanBasedStringConcatFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicUseSpanBasedStringConcat,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicUseSpanBasedStringConcatFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class UseSpanBasedStringConcatTests
    {
        #region Reports Diagnostic
        public static IEnumerable<object[]> Data_SingleViolationInOneBlock_CS
        {
            get
            {
                yield return new[]
                {
                    @"var _ = {|#0:foo.Substring(1) + bar|};",
                    @"var _ = string.Concat(foo.AsSpan(1), bar);"
                };
                yield return new[]
                {
                    @"var _ = {|#0:foo.Substring(1, 2) + bar|};",
                    @"var _ = string.Concat(foo.AsSpan(1, 2), bar);"
                };
                yield return new[]
                {
                    @"var _ = {|#0:foo + bar.Substring(1)|};",
                    @"var _ = string.Concat(foo, bar.AsSpan(1));"
                };
                yield return new[]
                {
                    @"var _ = {|#0:foo + bar.Substring(1) + baz|};",
                    @"var _ = string.Concat(foo, bar.AsSpan(1), baz);"
                };
                yield return new[]
                {
                    @"var _ = {|#0:foo.Substring(1) + bar.Substring(1) + baz.Substring(1)|};",
                    @"var _ = string.Concat(foo.AsSpan(1), bar.AsSpan(1), baz.AsSpan(1));"
                };
                yield return new[]
                {
                    @"var _ = {|#0:foo.Substring(1, 2) + bar + baz + baz.Substring(1, 2)|};",
                    @"var _ = string.Concat(foo.AsSpan(1, 2), bar, baz, baz.AsSpan(1, 2));"
                };
                yield return new[]
                {
                    @"Consume({|#0:foo + bar.Substring(1, 2)|});",
                    @"Consume(string.Concat(foo, bar.AsSpan(1, 2)));"
                };
                yield return new[]
                {
                    @"var _ = Fwd({|#0:foo.Substring(1) + bar|});",
                    @"var _ = Fwd(string.Concat(foo.AsSpan(1), bar));"
                };
                yield return new[]
                {
                    @"var _ = Fwd({|#0:foo.Substring(1) + bar.Substring(1)|});",
                    @"var _ = Fwd(string.Concat(foo.AsSpan(1), bar.AsSpan(1)));"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_SingleViolationInOneBlock_CS))]
        public Task SingleViolationInOneBlock_ReportedAndFixed_CS(string testStatements, string fixedStatements)
        {
            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(testStatements),
                FixedCode = CSUsings + CSWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_SingleViolationInOneBlock_VB
        {
            get
            {
                yield return new[]
                {
                    @"Dim s = {|#0:foo.Substring(1) & bar|}",
                    @"Dim s = String.Concat(foo.AsSpan(1), bar)"
                };
                yield return new[]
                {
                    @"Dim s = {|#0:foo.Substring(1, 2) & bar|}",
                    @"Dim s = String.Concat(foo.AsSpan(1, 2), bar)"
                };
                yield return new[]
                {
                    @"Dim s = {|#0:foo & bar.Substring(1)|}",
                    @"Dim s = String.Concat(foo, bar.AsSpan(1))"
                };
                yield return new[]
                {
                    @"Dim s = {|#0:foo & bar.Substring(1) & baz|}",
                    @"Dim s = String.Concat(foo, bar.AsSpan(1), baz)"
                };
                yield return new[]
                {
                    @"Dim s = {|#0:foo.Substring(1) & bar.Substring(1) & baz.Substring(1)|}",
                    @"Dim s = String.Concat(foo.AsSpan(1), bar.AsSpan(1), baz.AsSpan(1))"
                };
                yield return new[]
                {
                    @"Dim s = {|#0:foo.Substring(1, 2) & bar & baz & baz.Substring(1, 2)|}",
                    @"Dim s = String.Concat(foo.AsSpan(1, 2), bar, baz, baz.AsSpan(1, 2))"
                };
                yield return new[]
                {
                    @"Consume({|#0:foo & bar.Substring(1, 2)|})",
                    @"Consume(String.Concat(foo, bar.AsSpan(1, 2)))"
                };
                yield return new[]
                {
                    @"Dim s = Fwd({|#0:foo.Substring(1) & bar|})",
                    @"Dim s = Fwd(String.Concat(foo.AsSpan(1), bar))"
                };
                yield return new[]
                {
                    @"Dim s = Fwd({|#0:foo.Substring(1) & bar.Substring(1)|})",
                    @"Dim s = Fwd(String.Concat(foo.AsSpan(1), bar.AsSpan(1)))"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_SingleViolationInOneBlock_VB))]
        public Task SingleViolationInOneBlock_ReportedAndFixed_VB(string testStatement, string fixedStatement)
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(testStatement),
                FixedCode = VBUsings + VBWithBody(fixedStatement),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleViolationsInOneBlock_CS
        {
            get
            {
                yield return new object[]
                {
                    @"
string alpha = {|#0:foo.Substring(1, 2) + bar.Substring(1) + baz|};
string bravo = {|#1:foo + bar.Substring(3) + baz.Substring(1, 2)|};
string charlie = {|#2:foo + bar + baz.Substring(1, 2)|};
string delta = {|#3:foo.Substring(1) + bar.Substring(1) + baz.Substring(1, 2) + foo.Substring(1, 2)|};",
                    @"
string alpha = string.Concat(foo.AsSpan(1, 2), bar.AsSpan(1), baz);
string bravo = string.Concat(foo, bar.AsSpan(3), baz.AsSpan(1, 2));
string charlie = string.Concat(foo, bar, baz.AsSpan(1, 2));
string delta = string.Concat(foo.AsSpan(1), bar.AsSpan(1), baz.AsSpan(1, 2), foo.AsSpan(1, 2));",
                    new[] { 0, 1, 2, 3 }
                };
                yield return new object[]
                {
                    @"Consume({|#0:foo.Substring(1) + bar|}, {|#1:foo + bar.Substring(1)|});",
                    @"Consume(string.Concat(foo.AsSpan(1), bar), string.Concat(foo, bar.AsSpan(1)));",
                    new[] { 0, 1 }
                };
                yield return new object[]
                {
                    @"Consume(Fwd({|#0:foo.Substring(1) + bar|}), Fwd({|#1:foo + bar.Substring(1)|}));",
                    @"Consume(Fwd(string.Concat(foo.AsSpan(1), bar)), Fwd(string.Concat(foo, bar.AsSpan(1))));",
                    new[] { 0, 1 }
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleViolationsInOneBlock_CS))]
        public Task MultipleViolationsInOneBlock_AreReportedAndFixed_CS(string testStatements, string fixedStatements, int[] locations)
        {
            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(testStatements),
                FixedCode = CSUsings + CSWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            test.ExpectedDiagnostics.AddRange(locations.Select(x => VerifyCS.Diagnostic(Rule).WithLocation(x)));
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleViolationsInOneBlock_VB
        {
            get
            {
                yield return new object[]
                {
                    @"
Dim alpha = {|#0:foo.Substring(1, 2) & bar.Substring(1) & baz|}
Dim bravo = {|#1:foo & bar.Substring(3) & baz.Substring(1, 2)|}
Dim charlie = {|#2:foo & bar & baz.Substring(1, 2)|}
Dim delta = {|#3:foo.Substring(1) & bar.Substring(1) & baz.Substring(1, 2) & foo.Substring(1, 2)|}",
                    @"
Dim alpha = String.Concat(foo.AsSpan(1, 2), bar.AsSpan(1), baz)
Dim bravo = String.Concat(foo, bar.AsSpan(3), baz.AsSpan(1, 2))
Dim charlie = String.Concat(foo, bar, baz.AsSpan(1, 2))
Dim delta = String.Concat(foo.AsSpan(1), bar.AsSpan(1), baz.AsSpan(1, 2), foo.AsSpan(1, 2))",
                    new[] { 0, 1, 2, 3 }
                };
                yield return new object[]
                {
                    @"Consume({|#0:foo.Substring(1) & bar|}, {|#1:foo & bar.Substring(1)|})",
                    @"Consume(String.Concat(foo.AsSpan(1), bar), String.Concat(foo, bar.AsSpan(1)))",
                    new[] { 0, 1 }
                };
                yield return new object[]
                {
                    @"Consume(Fwd({|#0:foo.Substring(1) & bar|}), Fwd({|#1:foo & bar.Substring(1)|}))",
                    @"Consume(Fwd(String.Concat(foo.AsSpan(1), bar)), Fwd(String.Concat(foo, bar.AsSpan(1))))",
                    new[] { 0, 1 }
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleViolationsInOneBlock_VB))]
        public Task MultipleViolationsInOneBlock_AreReportedAndFixed_VB(string testStatements, string fixedStatements, int[] locations)
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(testStatements),
                FixedCode = VBUsings + VBWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
            };
            test.ExpectedDiagnostics.AddRange(locations.Select(x => VerifyVB.Diagnostic(Rule).WithLocation(x)));
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NestedViolations_CS
        {
            get
            {
                yield return new object[]
                {
                    @"
Consume({|#0:Fwd({|#1:foo + bar.Substring(1)|}) + baz.Substring(1)|});",
                    @"
Consume(string.Concat(Fwd(string.Concat(foo, bar.AsSpan(1))), baz.AsSpan(1)));",
                    new[] { 0, 1 },
                    2, 2, 2
                };
                yield return new object[]
                {
                    @"
var _ = {|#0:Fwd({|#1:foo.Substring(1) + bar.Substring(1)|}) + Fwd({|#2:foo.Substring(1) + bar|}).Substring(1) + Fwd({|#3:foo + bar.Substring(1)|})|};",
                    @"
var _ = string.Concat(Fwd(string.Concat(foo.AsSpan(1), bar.AsSpan(1))), Fwd(string.Concat(foo.AsSpan(1), bar)).AsSpan(1), Fwd(string.Concat(foo, bar.AsSpan(1))));",
                    new[] { 0, 1, 2, 3 },
                    4, 3, 3
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NestedViolations_CS))]
        public Task NestedViolations_AreReportedAndFixed_CS(
            string testStatements, string fixedStatements, int[] locations,
            int? incrementalIterations, int? fixAllInDocumentIterations, int? fixAllIterations)
        {
            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(testStatements),
                FixedCode = CSUsings + CSWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                NumberOfIncrementalIterations = incrementalIterations,
                NumberOfFixAllInDocumentIterations = fixAllInDocumentIterations,
                NumberOfFixAllIterations = fixAllIterations
            };
            test.ExpectedDiagnostics.AddRange(locations.Select(x => VerifyCS.Diagnostic(Rule).WithLocation(x)));
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NestedViolations_VB
        {
            get
            {
                yield return new object[]
                {
                    @"
Consume({|#0:Fwd({|#1:foo + bar.Substring(1)|}) + baz.Substring(1)|})",
                    @"
Consume(String.Concat(Fwd(String.Concat(foo, bar.AsSpan(1))), baz.AsSpan(1)))",
                    new[] { 0, 1 },
                    2, 2, 2
                };
                yield return new object[]
                {
                    @"
Dim s = {|#0:Fwd({|#1:foo.Substring(1) & bar.Substring(1)|}) & Fwd({|#2:foo.Substring(1) & bar|}).Substring(1) & Fwd({|#3:foo & bar.Substring(1)|})|}",
                    @"
Dim s = String.Concat(Fwd(String.Concat(foo.AsSpan(1), bar.AsSpan(1))), Fwd(String.Concat(foo.AsSpan(1), bar)).AsSpan(1), Fwd(String.Concat(foo, bar.AsSpan(1))))",
                    new[] { 0, 1, 2, 3 },
                    4, 3, 3
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NestedViolations_VB))]
        public Task NestedViolations_AreReportedAndFixed_VB(
            string testStatements, string fixedStatements, int[] locations,
            int? incrementalIterations, int? fixAllInDocumentIterations, int? fixAllIterations)
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(testStatements),
                FixedCode = VBUsings + VBWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                NumberOfIncrementalIterations = incrementalIterations,
                NumberOfFixAllIterations = fixAllInDocumentIterations,
                NumberOfFixAllInDocumentIterations = fixAllIterations
            };
            test.ExpectedDiagnostics.AddRange(locations.Select(x => VerifyVB.Diagnostic(Rule).WithLocation(x)));
            return test.RunAsync();
        }

        [Fact]
        public Task ConditionalSubstringAccess_IsFlaggedButNotFixed_CS()
        {
            string statements = @"var s = {|#0:foo?.Substring(1) + bar|};";
            string source = CSUsings + CSWithBody(statements);

            var test = new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ConditionalSubstringAccess_IsFlaggedButNotFixed_VB()
        {
            string statements = @"Dim s = {|#0:foo?.Substring(1) & bar|}";
            string source = VBUsings + VBWithBody(statements);

            var test = new VerifyVB.Test
            {
                TestCode = source,
                FixedCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task MissingSystemImport_IsAdded_WhenAbsent_CS()
        {
            var test = new VerifyCS.Test
            {
                TestCode = CSWithBody(@"var _ = {|#0:foo + bar.Substring(1)|};"),
                FixedCode = $"{Environment.NewLine}{CSUsings}{Environment.NewLine}" + CSWithBody(@"var _ = string.Concat(foo, bar.AsSpan(1));"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        //  Visual Basic supports implicit global imports. By default, 'System' is added as a global
        //  import when you create a project in Visual Studio.
        [Fact]
        public Task MissingSystemImport_IsNotAdded_WhenIncludedInGlobalImports_VB()
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBWithBody(@"Dim s = {|#0:foo & bar.Substring(1)|}"),
                FixedCode = VBWithBody(@"Dim s = String.Concat(foo, bar.AsSpan(1))"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            test.SolutionTransforms.Add((s, id) =>
            {
                var project = s.Projects.Single();
                var options = project.CompilationOptions as VisualBasicCompilationOptions;
                var globalSystemImport = GlobalImport.Parse(nameof(System));
                options = options.WithGlobalImports(globalSystemImport);
                return s.WithProjectCompilationOptions(project.Id, options);
            });
            return test.RunAsync();
        }

        //  We must add 'Imports System' if it is not included as a global import.
        [Fact]
        public Task MissingSystemImport_IsAdded_WhenAbsentFromGlobalImports_VB()
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBWithBody(@"Dim s = {|#0:foo & bar.Substring(1)|}"),
                FixedCode = $"{Environment.NewLine}{VBUsings}{Environment.NewLine}" + VBWithBody(@"Dim s = String.Concat(foo, bar.AsSpan(1))"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"Substring(startIndex: 1)", @"AsSpan(start: 1)")]
        [InlineData(@"Substring(startIndex: 1, length: 2)", @"AsSpan(start: 1, length: 2)")]
        [InlineData(@"Substring(1, length: 2)", @"AsSpan(1, length: 2)")]
        [InlineData(@"Substring(startIndex: 1, 2)", @"AsSpan(start: 1, 2)")]
        [InlineData(@"Substring(length: 2, startIndex: 1)", @"AsSpan(length: 2, start: 1)")]
        public Task NamedSubstringArguments_ArePreserved_CS(string substring, string asSpan)
        {
            string testStatements = $@"var s = {{|#0:foo.{substring} + bar|}};";
            string fixedStatements = $@"var s = string.Concat(foo.{asSpan}, bar);";

            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(testStatements),
                FixedCode = CSUsings + CSWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"Substring(startIndex:=1)", @"AsSpan(start:=1)")]
        [InlineData(@"Substring(startIndex:=1, length:=2)", @"AsSpan(start:=1, length:=2)")]
        [InlineData(@"Substring(1, length:=2)", @"AsSpan(1, length:=2)")]
        [InlineData(@"Substring(startIndex:=1, 2)", @"AsSpan(start:=1, 2)")]
        [InlineData(@"Substring(length:=2, startIndex:=1)", @"AsSpan(length:=2, start:=1)")]
        public Task NamedSubstringArguments_ArePreserved_VB(string substring, string asSpan)
        {
            string testStatements = $@"Dim s = {{|#0:foo.{substring} & bar|}}";
            string fixedStatements = $@"Dim s = String.Concat(foo.{asSpan}, bar)";

            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(testStatements),
                FixedCode = VBUsings + VBWithBody(fixedStatements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"foo.Substring(1) + (string)explicitTo", @"string.Concat(foo.AsSpan(1), (string)explicitTo)")]
        [InlineData(@"(string)explicitTo + foo.Substring(1)", @"string.Concat((string)explicitTo, foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) + (string)thing", @"string.Concat(foo.AsSpan(1), (string)thing)")]
        [InlineData(@"(string)thing + foo.Substring(1)", @"string.Concat((string)thing, foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) + (thing as string)", @"string.Concat(foo.AsSpan(1), thing as string)")]
        [InlineData(@"(thing as string) + foo.Substring(1)", @"string.Concat(thing as string, foo.AsSpan(1))")]
        public Task ExplicitConversions_ArePreserved_CS(string testExpression, string fixedExpression)
        {
            string helperTypes = @"
public class ExplicitTo
{
    public static explicit operator string(ExplicitTo operand) => operand?.ToString();
}

public class ExplicitFrom
{
    public static explicit operator ExplicitFrom(string operand) => new ExplicitFrom();
}";
            string format = @"
var explicitTo = new ExplicitTo();
object thing = bar;
var s = {0};";
            var culture = CultureInfo.InvariantCulture;
            string testStatements = string.Format(culture, format, $@"{{|#0:{testExpression}|}}");
            string fixedStatements = string.Format(culture, format, fixedExpression);

            var test = new VerifyCS.Test
            {
                TestState = { Sources = { CSUsings + CSWithBody(testStatements), helperTypes } },
                FixedState = { Sources = { CSUsings + CSWithBody(fixedStatements), helperTypes } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"foo.Substring(1) & CType(explicitTo, String)", @"String.Concat(foo.AsSpan(1), CType(explicitTo, String))")]
        [InlineData(@"CType(explicitTo, String) & foo.Substring(1)", @"String.Concat(CType(explicitTo, String), foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) & DirectCast(thing, String)", @"String.Concat(foo.AsSpan(1), DirectCast(thing, String))")]
        [InlineData(@"DirectCast(thing, String) & foo.Substring(1)", @"String.Concat(DirectCast(thing, String), foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) & TryCast(thing, String)", @"String.Concat(foo.AsSpan(1), TryCast(thing, String))")]
        [InlineData(@"TryCast(thing, String) & foo.Substring(1)", @"String.Concat(TryCast(thing, String), foo.AsSpan(1))")]
        public Task ExplicitConversions_ArePreserved_VB(string testExpression, string fixedExpression)
        {
            string helperTypes = @"
Public Class ExplicitTo

    Public Shared Narrowing Operator CType(operand As ExplicitTo) As String
        Return New ExplicitTo()
    End Operator
End Class

Public Class ExplicitFrom
    Public Shared Narrowing Operator CType(operand As String) As ExplicitFrom
        Return New ExplicitFrom()
    End Operator
End Class";
            string format = @"
Dim explicitTo = New ExplicitTo()
Dim thing As Object = bar
Dim s = {0}";
            var culture = CultureInfo.InvariantCulture;
            string testStatements = string.Format(culture, format, $@"{{|#0:{testExpression}|}}");
            string fixedStatements = string.Format(culture, format, fixedExpression);

            var test = new VerifyVB.Test
            {
                TestState = { Sources = { VBUsings + VBWithBody(testStatements), helperTypes } },
                FixedState = { Sources = { VBUsings + VBWithBody(fixedStatements), helperTypes } },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        //  No C# case because C# has only one concat operator.
        [Theory]
        [InlineData(@"foo.Substring(1) & bar + baz", @"String.Concat(foo.AsSpan(1), bar, baz)")]
        [InlineData(@"foo & bar.Substring(1) + baz", @"String.Concat(foo, bar.AsSpan(1), baz)")]
        [InlineData(@"foo & bar + baz.Substring(1)", @"String.Concat(foo, bar, baz.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) + bar & baz", @"String.Concat(foo.AsSpan(1), bar, baz)")]
        [InlineData(@"foo + bar.Substring(1) & baz", @"String.Concat(foo, bar.AsSpan(1), baz)")]
        [InlineData(@"foo + bar & baz.Substring(1)", @"String.Concat(foo, bar, baz.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) & bar + baz & baz.Substring(1)", @"String.Concat(foo.AsSpan(1), bar, baz, baz.AsSpan(1))")]
        [InlineData(@"foo & bar.Substring(1) + baz & foo", @"String.Concat(foo, bar.AsSpan(1), baz, foo)")]
        [InlineData(@"foo.Substring(1) & bar + baz & foo", @"String.Concat(foo.AsSpan(1), bar, baz, foo)")]
        [InlineData(@"foo & bar + baz & foo.Substring(1)", @"String.Concat(foo, bar, baz, foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) + bar & baz + baz.Substring(1)", @"String.Concat(foo.AsSpan(1), bar, baz, baz.AsSpan(1))")]
        [InlineData(@"foo + bar.Substring(1) & baz + foo", @"String.Concat(foo, bar.AsSpan(1), baz, foo)")]
        [InlineData(@"foo.Substring(1) + bar & baz + foo", @"String.Concat(foo.AsSpan(1), bar, baz, foo)")]
        [InlineData(@"foo + bar & baz + foo.Substring(1)", @"String.Concat(foo, bar, baz, foo.AsSpan(1))")]
        public Task MixedAddAndConcatenateOperatorChains_AreReportedAndFixed_VB(string testExpression, string fixedExpression)
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody($@"Dim s = {{|#0:{testExpression}|}}"),
                FixedCode = VBUsings + VBWithBody($@"Dim s = {fixedExpression}"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"foo.Substring(1) + 'A'", @"string.Concat(foo.AsSpan(1), ""A"")")]
        [InlineData(@"'A' + foo.Substring(1)", @"string.Concat(""A"", foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) + bar + 'A'", @"string.Concat(foo.AsSpan(1), bar, ""A"")")]
        [InlineData(@"foo.Substring(1) + 'A' + bar", @"string.Concat(foo.AsSpan(1), ""A"", bar)")]
        [InlineData(@"foo + 'A' + bar.Substring(1)", @"string.Concat(foo, ""A"", bar.AsSpan(1))")]
        [InlineData(@"foo + bar.Substring(1) + 'A'", @"string.Concat(foo, bar.AsSpan(1), ""A"")")]
        public Task CharLiterals_AreConvertedToStringLiterals_CS(string testExpression, string fixedExpression)
        {
            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody($"string s = {{|#0:{testExpression}|}};"),
                FixedCode = CSUsings + CSWithBody($"string s = {fixedExpression};"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyCS.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"foo.Substring(1) & ""A""c", @"String.Concat(foo.AsSpan(1), ""A"")")]
        [InlineData(@"""A""c & foo.Substring(1)", @"String.Concat(""A"", foo.AsSpan(1))")]
        [InlineData(@"foo.Substring(1) & bar & ""A""c", @"String.Concat(foo.AsSpan(1), bar, ""A"")")]
        [InlineData(@"foo.Substring(1) & ""A""c & bar", @"String.Concat(foo.AsSpan(1), ""A"", bar)")]
        [InlineData(@"foo & ""A""c & bar.Substring(1)", @"String.Concat(foo, ""A"", bar.AsSpan(1))")]
        [InlineData(@"foo & bar.Substring(1) & ""A""c", @"String.Concat(foo, bar.AsSpan(1), ""A"")")]
        public Task CharLiterals_AreConvertedToStringLiterals_VB(string testExpression, string fixedExpression)
        {
            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody($"Dim s = {{|#0:{testExpression}|}}"),
                FixedCode = VBUsings + VBWithBody($"Dim s = {fixedExpression}"),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                ExpectedDiagnostics = { VerifyVB.Diagnostic(Rule).WithLocation(0) }
            };
            return test.RunAsync();
        }
        #endregion

        #region No Diagnostic
        [Theory]
        [InlineData("foo.Substring(1) + foo + foo + bar + baz")]
        [InlineData("foo + foo.Substring(1) + bar + baz + baz")]
        [InlineData("foo.Substring(1) + bar.Substring(1) + baz.Substring(1) + bar.Substring(1) + foo.Substring(1)")]
        [InlineData("foo.Substring(1) + bar + baz + foo.Substring(1) + bar + baz")]
        [InlineData("foo + bar.Substring(1) + baz + foo + bar.Substring(1) + baz")]
        [InlineData("foo.Substring(1) + bar + baz.Substring(1) + foo.Substring(1) + bar + baz.Substring(1)")]
        public Task TooManyArguments_NoDiagnostic_CS(string expression)
        {
            string statements = $@"var s = {expression};";

            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(statements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("foo.Substring(1) & foo & foo & bar & baz")]
        [InlineData("foo & foo.Substring(1) & bar & baz & baz")]
        [InlineData("foo.Substring(1) & bar.Substring(1) & baz.Substring(1) & bar.Substring(1) & foo.Substring(1)")]
        [InlineData("foo.Substring(1) & bar & baz & foo.Substring(1) & bar & baz")]
        [InlineData("foo & bar.Substring(1) & baz & foo & bar.Substring(1) & baz")]
        [InlineData("foo.Substring(1) & bar & baz.Substring(1) & foo.Substring(1) & bar & baz.Substring(1)")]
        public Task TooManyArguments_NoDiagnostic_VB(string expression)
        {
            string statements = $@"Dim s = {expression}";

            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(statements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("foo + bar")]
        [InlineData("foo + bar + baz")]
        [InlineData("foo + bar.ToUpper()")]
        [InlineData("foo.ToLower() + bar")]
        public Task NoSubstringInvocations_NoDiagnostic_CS(string expression)
        {
            string statements = $@"var s = {expression};";

            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(statements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("foo & bar")]
        [InlineData("foo & bar & baz")]
        [InlineData("foo & bar.ToUpper()")]
        [InlineData("foo.ToLower() & bar")]
        public Task NoSubstringInvocations_NoDiagnostic_VB(string expression)
        {
            string statements = $@"Dim s = {expression}";

            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(statements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        //  No VB case because VB can't overload operators.
        [Theory]
        [InlineData(@"foo.Substring(1) + evil")]
        [InlineData(@"evil + foo.Substring(1)")]
        [InlineData(@"foo + evil + bar.Substring(1)")]
        [InlineData(@"foo.Substring(1) + evil + bar")]
        [InlineData(@"foo.Substring(1) + evil + bar + baz")]
        [InlineData(@"foo + bar + evil + baz.Substring(1)")]
        [InlineData(@"foo + evil + bar.Substring(1) + evil")]
        [InlineData(@"foo + evil + bar.Substring(1) + evil + baz.Substring(1)")]
        public Task OverloadedAddOperator_NoDiagnostic_CS(string expression)
        {
            string statements = $@"
var evil = new EvilOverloads();
var e = {expression};";

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        EvilOverloads,
                        CSUsings + CSWithBody(statements)
                    }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"foo.Substring(1) + thing")]
        [InlineData(@"thing + foo.Substring(1)")]
        [InlineData(@"foo.Substring(1) + count")]
        [InlineData(@"count + foo.Substring(1)")]
        [InlineData(@"foo.Substring(1) + charvar")]
        [InlineData(@"charvar + foo.Substring(1)")]
        [InlineData(@"foo.Substring(1) + bar + thing")]
        [InlineData(@"foo + bar.Substring(1) + thing")]
        [InlineData(@"foo.Substring(1) + thing + bar.Substring(1)")]
        [InlineData(@"foo.Substring(1) + bar.Substring(1) + thing")]
        public Task WithNonStringNonCharLiteralOperands_NoDiagnostic_CS(string expression)
        {
            string statements = $@"
object thing = new object();
int count = 17;
char charvar = 'H';
string s = {expression};";

            var test = new VerifyCS.Test
            {
                TestCode = CSUsings + CSWithBody(statements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData(@"foo.Substring(1) & thing")]
        [InlineData(@"thing & foo.Substring(1)")]
        [InlineData(@"foo.Substring(1) & count")]
        [InlineData(@"count & foo.Substring(1)")]
        [InlineData(@"foo.Substring(1) & charvar")]
        [InlineData(@"charvar & foo.Substring(1)")]
        [InlineData(@"foo.Substring(1) & bar & thing")]
        [InlineData(@"foo & bar.Substring(1) & thing")]
        [InlineData(@"foo.Substring(1) & thing & bar.Substring(1)")]
        [InlineData(@"foo.Substring(1) & bar.Substring(1) & thing")]
        public Task WithNonStringNonCharLiteralOperands_NoDiagnostic_VB(string expression)
        {
            string statements = $@"
Dim thing As Object = New Object()
Dim count As Integer = 17
Dim charvar As Char = ""H""
Dim s As String = {expression}";

            var test = new VerifyVB.Test
            {
                TestCode = VBUsings + VBWithBody(statements),
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }
        #endregion

        #region Helpers
        private static DiagnosticDescriptor Rule => UseSpanBasedStringConcat.Rule;
        private const string CSUsings = @"using System;";
        private const string VBUsings = @"Imports System";
        private const string EvilOverloads = @"    
public class EvilOverloads
{
    public static EvilOverloads operator +(EvilOverloads left, string right) => left;
    public static EvilOverloads operator +(string left, EvilOverloads right) => right;
    public static EvilOverloads operator +(EvilOverloads left, EvilOverloads right) => left;
}";

        private static string CSWithBody(string statements)
        {
            return @"
public class Testopolis
{
    private void Consume(string consumed) { }
    private void Consume(string s1, string s2) { }
    private void Consume(string s1, string s2, string s3) { }
    private string Fwd(string arg) => arg;
    private string Transform(string first, string second) => second;
    private string Transform(string first, string second, string third) => first;
    private string Produce() => ""Hello World"";

    public void FrobThem(string foo, string bar, string baz)
    {
" + IndentLines(statements, "        ") + @"
    }
}";
        }

        private static string VBWithBody(string statements)
        {
            return @"
Public Class Testopolis
    Private Sub Consume(consumed As String)
    End Sub
    Private Sub Consume(s1 As String, s2 As String)
    End Sub
    Private Sub Consume(s1 As String, s2 As String, s3 As String)
    End Sub
    Private Function Fwd(arg As String) As String
        Return arg
    End Function
    Private Function Transform(first As String, second As String) As String
        Return second
    End Function
    Private Function Transform(first As String, second As String, third As String) As String
        Return first
    End Function
    Private Function Produce() As String
        Return ""Hello World""
    End Function

    Public Sub FrobThem(foo As String, bar As String, baz As String)
" + IndentLines(statements, "        ") + @"
    End Sub
End Class";
        }

        private static string IndentLines(string body, string indent)
        {
            return indent + body.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent, StringComparison.Ordinal);
        }
        #endregion
    }
}
