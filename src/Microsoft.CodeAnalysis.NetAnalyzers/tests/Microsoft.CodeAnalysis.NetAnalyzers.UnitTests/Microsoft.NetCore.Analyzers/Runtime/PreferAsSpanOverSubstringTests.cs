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
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpPreferAsSpanOverSubstringFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.PreferAsSpanOverSubstring,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicPreferAsSpanOverSubstringFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class PreferAsSpanOverSubstringTests
    {
        public static IEnumerable<object[]> Data_SubstringAsSpanPair_CS
        {
            get
            {
                yield return new[] { @"Substring(1)", @"AsSpan(1)" };
                yield return new[] { @"Substring(1, 2)", @"AsSpan(1, 2)" };
                yield return new[] { @"Substring(startIndex: 1)", @"AsSpan(start: 1)" };
                yield return new[] { @"Substring(startIndex: 1, 2)", @"AsSpan(start: 1, 2)" };
                yield return new[] { @"Substring(1, length: 2)", @"AsSpan(1, length: 2)" };
                yield return new[] { @"Substring(startIndex: 1, length: 2)", @"AsSpan(start: 1, length: 2)" };
                yield return new[] { @"Substring(length: 2, startIndex: 1)", @"AsSpan(length: 2, start: 1)" };
            }
        }

        public static IEnumerable<object[]> Data_SubstringAsSpanPair_VB
        {
            get
            {
                yield return new[] { @"Substring(1)", @"AsSpan(1)" };
                yield return new[] { @"Substring(1, 2)", @"AsSpan(1, 2)" };
                yield return new[] { @"Substring(startIndex:=1)", @"AsSpan(start:=1)" };
                yield return new[] { @"Substring(startIndex:=1, 2)", @"AsSpan(start:=1, 2)" };
                yield return new[] { @"Substring(1, length:=2)", @"AsSpan(1, length:=2)" };
                yield return new[] { @"Substring(startIndex:=1, length:=2)", @"AsSpan(start:=1, length:=2)" };
                yield return new[] { @"Substring(length:=2, startIndex:=1)", @"AsSpan(length:=2, start:=1)" };
            }
        }

        [Theory]
        [MemberData(nameof(Data_SubstringAsSpanPair_CS))]
        public Task SingleArgumentStaticMethod_ReportsDiagnostic_CS(string substring, string asSpan)
        {
            string thing = @"
using System;

public class Thing
{
    public static void Consume(string text) { }
    public static void Consume(ReadOnlySpan<char> span) { }
}";
            string testStatements = WithKey($"Thing.Consume(foo.{substring})", 0) + ';';
            string fixedStatements = $"Thing.Consume(foo.{asSpan});";

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { thing, CS.WithBody(testStatements) },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { thing, CS.WithBody(fixedStatements) }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_SubstringAsSpanPair_VB))]
        public Task SingleArgumentStaticMethod_ReportsDiagnostic_VB(string substring, string asSpan)
        {
            //  'Thing' needs to be in a C# project because VB doesn't support spans in exposed APIs.
            string thing = @"
using System;

public class Thing
{
    public static void Consume(string text) { }
    public static void Consume(ReadOnlySpan<char> span) { }
}";
            var thingProject = new ProjectState("ThingProject", LanguageNames.CSharp, "thing", "cs")
            {
                Sources = { thing }
            };
            string testStatements = WithKey($"Thing.Consume(foo.{substring})", 0);
            string fixedStatements = $"Thing.Consume(foo.{asSpan})";

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { VB.WithBody(testStatements) },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { VB.WithBody(fixedStatements) },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_SubstringAsSpanPair_CS))]
        public Task SingleArgumentInstanceMethod_ReportsDiagnostic_CS(string substring, string asSpan)
        {
            string thing = @"
using System;

public class Thing
{
    public void Consume(string text) { }
    public void Consume(ReadOnlySpan<char> span) { }
}";
            string fields = @"
public partial class Body
{
    private Thing thing = new Thing();
}";
            string testCode = CS.WithBody(WithKey($"thing.Consume(foo.{substring})", 0) + ';');
            string fixedCode = CS.WithBody($"thing.Consume(foo.{asSpan});");

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, thing, fields },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, thing, fields }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_SubstringAsSpanPair_VB))]
        public Task SingleArgumentInstanceMethod_ReportsDiagnostic_VB(string substring, string asSpan)
        {
            //  'Thing' needs to be in a C# project besause VB doesn't support spans in exposed APIs.
            string thing = @"
using System;

public class Thing
{
    public void Consume(string text) { }
    public void Consume(ReadOnlySpan<char> span) { }
}";
            var thingProject = new ProjectState("ThingProject", LanguageNames.CSharp, "thing", "cs")
            {
                Sources = { thing }
            };
            string fields = @"
Partial Public Class Body
    
    Private thing As Thing = New Thing()
End Class";
            string testCode = VB.WithBody(WithKey($"thing.Consume(foo.{substring})", 0));
            string fixedCode = VB.WithBody($"thing.Consume(foo.{asSpan})");

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode, fields },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, fields },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleArguments_WithAvailableSpanOverloads
        {
            get
            {
                const string usings = @"
using System;
using Roschar = System.ReadOnlySpan<char>;";
                string thing = usings + @"
public class Thing
{
    public static void Consume(string text, int num) { }
    public static void Consume(Roschar span, int num) { }
    public static void Consume(double[] data, string text, int num) { }
    public static void Consume(double[] data, Roschar text, int num) { }
    public static void Consume(string text1, string text2, int num) { }
    public static void Consume(Roschar span1, Roschar span2, int num) { }
    public static void Consume(Roschar span1, string text2, int num) { }
    public static void Consume(string text1, Roschar span2, int num) { }
    public static void Consume(string text1, int num, string text2) { }
    public static void Consume(string text1, int num, Roschar span2) { }
    public static void Consume(Roschar span1, int num, string text2) { }
    public static void Consume(Roschar span1, int num, Roschar span2) { }
}";
                yield return new[] { thing, @"foo.Substring(1), 17", @"foo.AsSpan(1), 17" };
                yield return new[] { thing, @"_data, foo.Substring(1, 2), 17", @"_data, foo.AsSpan(1, 2), 17" };
                yield return new[] { thing, @"foo.Substring(1), foo.Substring(1, 2), 17", @"foo.AsSpan(1), foo.AsSpan(1, 2), 17" };
                yield return new[] { thing, @"foo.Substring(1), foo, 17", @"foo.AsSpan(1), foo, 17" };
                yield return new[] { thing, @"foo, foo.Substring(1), 17", @"foo, foo.AsSpan(1), 17" };
                yield return new[] { thing, @"foo, 17, foo.Substring(1)", @"foo, 17, foo.AsSpan(1)" };
                yield return new[] { thing, @"foo.Substring(1), 17, foo", @"foo.AsSpan(1), 17, foo" };
                yield return new[] { thing, @"foo.Substring(1), 17, foo.Substring(1, 2)", @"foo.AsSpan(1), 17, foo.AsSpan(1, 2)" };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleArguments_WithAvailableSpanOverloads))]
        public Task MultipleArguments_WithAvailableSpanOverloads_ReportsDiagnostic_CS(string receiverClass, string testArguments, string fixedArguments)
        {
            string fields = @"
public partial class Body
{
    private double[] _data = new[] { 3.14159, 2.71828 };
}";
            string testCode = CS.WithBody(WithKey($"Thing.Consume({testArguments})", 0) + ';');
            string fixedCode = CS.WithBody($"Thing.Consume({fixedArguments});");

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiverClass, fields },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, receiverClass, fields }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_MultipleArguments_WithAvailableSpanOverloads))]
        public Task MultipleArguments_WithAvailableSpanOverloads_ReportsDiagnostic_VB(string receiverClass, string testArguments, string fixedArguments)
        {
            //  Use C# project because VB doesn't support spans in APIs.
            var thingProject = new ProjectState("ThingProject", LanguageNames.CSharp, "thing", "cs")
            {
                Sources = { receiverClass }
            };
            string fields = @"
Partial Public Class Body

    Private _data As Double() = {3.14159, 2.71828}
End Class";
            string testCode = VB.WithBody(WithKey($"Thing.Consume({testArguments})", 0));
            string fixedCode = VB.WithBody($"Thing.Consume({fixedArguments})");

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode, fields },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, fields },
                    AdditionalProjects = { { thingProject.Name, thingProject } },
                    AdditionalProjectReferences = { thingProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NamedArguments_CS
        {
            get
            {
                string usings = @"
using System;
using Roschar = System.ReadOnlySpan<char>;";
                string thing = usings + @"
public class Thing
{
    public static void Consume(string text, int n) { }
    public static void Consume(Roschar span, int c) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text: foo.Substring(1), 7)",
                    @"Thing.Consume(span: foo.AsSpan(1), 7)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(foo.Substring(1), n: 7)",
                    @"Thing.Consume(foo.AsSpan(1), c: 7)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text: foo.Substring(1), n: 7)",
                    @"Thing.Consume(span: foo.AsSpan(1), c: 7)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(n: 7, text: foo.Substring(1))",
                    @"Thing.Consume(c: 7, span: foo.AsSpan(1))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(n: 7, text: foo.Substring(length: 2, startIndex: 1))",
                    @"Thing.Consume(c: 7, span: foo.AsSpan(length: 2, start: 1))"
                };

                thing = usings + @"
public class Thing
{
    public static void Consume(string text1A, string text2A) { }
    public static void Consume(Roschar span1B, string text2B) { }
    public static void Consume(string text1C, Roschar span2C) { }
    public static void Consume(Roschar span1D, Roschar span2D) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text1A: foo, text2A: foo.Substring(2))",
                    @"Thing.Consume(text1C: foo, span2C: foo.AsSpan(2))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text2A: foo.Substring(2), text1A: foo)",
                    @"Thing.Consume(span2C: foo.AsSpan(2), text1C: foo)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text1A: foo.Substring(1), text2A: foo)",
                    @"Thing.Consume(span1B: foo.AsSpan(1), text2B: foo)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text2A: foo, text1A: foo.Substring(1))",
                    @"Thing.Consume(text2B: foo, span1B: foo.AsSpan(1))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text1A: foo.Substring(1), text2A: foo.Substring(2))",
                    @"Thing.Consume(span1D: foo.AsSpan(1), span2D: foo.AsSpan(2))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text2A: foo.Substring(2), text1A: foo.Substring(1))",
                    @"Thing.Consume(span2D: foo.AsSpan(2), span1D: foo.AsSpan(1))"
                };

                thing = usings + @"
public class Thing
{
    public static void Consume(int n1A, string text2A, string text3A) { }
    public static void Consume(int n1B, Roschar span2B, Roschar span3B) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text3A: foo.Substring(3), n1A: 7, text2A: foo.Substring(2))",
                    @"Thing.Consume(span3B: foo.AsSpan(3), n1B: 7, span2B: foo.AsSpan(2))"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NamedArguments_CS))]
        public Task NamedArguments_AreHandledCorrectly_CS(string receiverClass, string testExpression, string fixedExpression)
        {
            string testCode = CS.WithBody(WithKey(testExpression, 0) + ';');
            string fixedCode = CS.WithBody(fixedExpression + ';');

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiverClass },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, receiverClass }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NamedArguments_VB
        {
            get
            {
                string usings = @"
using System;
using Roschar = System.ReadOnlySpan<char>;";
                string thing = usings + @"
public class Thing
{
    public static void Consume(string text, int n) { }
    public static void Consume(Roschar span, int c) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text:=foo.Substring(1), 7)",
                    @"Thing.Consume(span:=foo.AsSpan(1), 7)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(foo.Substring(1), n:=7)",
                    @"Thing.Consume(foo.AsSpan(1), c:=7)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text:=foo.Substring(1), n:=7)",
                    @"Thing.Consume(span:=foo.AsSpan(1), c:=7)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(n:=7, text:=foo.Substring(1))",
                    @"Thing.Consume(c:=7, span:=foo.AsSpan(1))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(n:=7, text:=foo.Substring(length:=2, startIndex:=1))",
                    @"Thing.Consume(c:=7, span:=foo.AsSpan(length:=2, start:=1))"
                };

                thing = usings + @"
public class Thing
{
    public static void Consume(string text1A, string text2A) { }
    public static void Consume(Roschar span1B, string text2B) { }
    public static void Consume(string text1C, Roschar span2C) { }
    public static void Consume(Roschar span1D, Roschar span2D) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text1A:=foo, text2A:=foo.Substring(2))",
                    @"Thing.Consume(text1C:=foo, span2C:=foo.AsSpan(2))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text2A:=foo.Substring(2), text1A:=foo)",
                    @"Thing.Consume(span2C:=foo.AsSpan(2), text1C:=foo)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text1A:=foo.Substring(1), text2A:=foo)",
                    @"Thing.Consume(span1B:=foo.AsSpan(1), text2B:=foo)"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text2A:=foo, text1A:=foo.Substring(1))",
                    @"Thing.Consume(text2B:=foo, span1B:=foo.AsSpan(1))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text1A:=foo.Substring(1), text2A:=foo.Substring(2))",
                    @"Thing.Consume(span1D:=foo.AsSpan(1), span2D:=foo.AsSpan(2))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text2A:=foo.Substring(2), text1A:=foo.Substring(1))",
                    @"Thing.Consume(span2D:=foo.AsSpan(2), span1D:=foo.AsSpan(1))"
                };

                thing = usings + @"
public class Thing
{
    public static void Consume(int n1A, string text2A, string text3A) { }
    public static void Consume(int n1B, Roschar span2B, Roschar span3B) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(text3A:=foo.Substring(3), n1A:=7, text2A:=foo.Substring(2))",
                    @"Thing.Consume(span3B:=foo.AsSpan(3), n1B:=7, span2B:=foo.AsSpan(2))"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NamedArguments_VB))]
        public Task NamedArguments_AreHandledCorrectly_VB(string receiverClass, string testExpression, string fixedExpression)
        {
            string testCode = VB.WithBody(WithKey(testExpression, 0));
            string fixedCode = VB.WithBody(fixedExpression);
            var receiverProject = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "vb")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_WhenRoscharOverloadAlreadySelected_SubstringConvertedToAsSpan
        {
            get
            {
                string thing = CS.Usings + @"
public class Thing
{
    public static void Consume(Roschar span) { }
    public static void Consume(Roschar span1, Roschar span2) { }
    public static void Consume(Roschar span1, Roschar span2, Roschar span3) { }
}";
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(foo.Substring(1))",
                    @"Thing.Consume(foo.AsSpan(1))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(foo.Substring(1), foo.Substring(2))",
                    @"Thing.Consume(foo.AsSpan(1), foo.AsSpan(2))"
                };
                yield return new[]
                {
                    thing,
                    @"Thing.Consume(foo.Substring(1), foo.Substring(2), foo.Substring(3))",
                    @"Thing.Consume(foo.AsSpan(1), foo.AsSpan(2), foo.AsSpan(3))"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_WhenRoscharOverloadAlreadySelected_SubstringConvertedToAsSpan))]
        public Task WhenRoscharOverloadAlreadySelected_SubstringConvertedToAsSpan_CS(string receiverClass, string testExpression, string fixedExpression)
        {
            string testCode = CS.WithBody(WithKey(testExpression, 0) + ';');
            string fixedCode = CS.WithBody(fixedExpression + ';');

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiverClass },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, receiverClass }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_WhenRoscharOverloadAlreadySelected_SubstringConvertedToAsSpan))]
        public Task WhenRoscharOverloadAlreadySelected_SubstringConvertedToAsSpan_VB(string receiverClass, string testExpression, string fixedExpression)
        {
            string testCode = VB.WithBody(WithKey(testExpression, 0));
            string fixedCode = VB.WithBody(fixedExpression);
            var receiverProject = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NestedViolations
        {
            get
            {
                string receiver = CS.Usings + @"
public class C
{
    public static string Fwd(string text) => throw null;
    public static string Fwd(Roschar span) => throw null;
    public static string Fwd(string text1, string text2) => throw null;
    public static string Fwd(Roschar span1, Roschar span2) => throw null;

    public static void Consume(string text) { }
    public static void Consume(Roschar span) { }
    public static void Consume(string text1, string text2) { }
    public static void Consume(Roschar span1, Roschar span2) { }
}";
                yield return new object[]
                {
                    receiver,
                    @"{|#0:C.Consume({|#1:C.Fwd(foo.Substring(1))|}.Substring(2))|}",
                    @"C.Consume(C.Fwd(foo.AsSpan(1)).AsSpan(2))",
                    new[] { 0, 1 },
                    2
                };
                yield return new object[]
                {
                    receiver,
                    @"{|#0:C.Consume({|#1:C.Fwd(foo.Substring(1), foo.Substring(2))|}.Substring(3), foo.Substring(4))|}",
                    @"C.Consume(C.Fwd(foo.AsSpan(1), foo.AsSpan(2)).AsSpan(3), foo.AsSpan(4))",
                    new[] { 0, 1 },
                    2
                };
                yield return new object[]
                {
                    receiver,
                    @"{|#0:C.Consume({|#1:C.Fwd(foo.Substring(1), {|#2:C.Fwd(foo.Substring(2))|}.Substring(3))|}.Substring(4))|}",
                    @"C.Consume(C.Fwd(foo.AsSpan(1), C.Fwd(foo.AsSpan(2)).AsSpan(3)).AsSpan(4))",
                    new[] { 0, 1, 2 },
                    3
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NestedViolations))]
        public Task NestedViolations_AreAllReportedAndFixed_CS(
            string receiverClass, string testExpression, string fixedExpression, int[] locations,
            int? incrementalIterations)
        {
            string testCode = CS.WithBody(testExpression + ';');
            string fixedCode = CS.WithBody(fixedExpression + ';');

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiverClass }
                },
                FixedState =
                {
                    Sources = { fixedCode, receiverClass }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                NumberOfIncrementalIterations = incrementalIterations,
            };
            test.TestState.ExpectedDiagnostics.AddRange(locations.Select(x => CS.DiagnosticAt(x)));
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_NestedViolations))]
        public Task NestedViolations_AreAllReportedAndFixed_VB(
            string receiverClass, string testExpression, string fixedExpression, int[] locations,
            int? incrementalIterations)
        {
            string testCode = VB.WithBody(testExpression);
            string fixedCode = VB.WithBody(fixedExpression);
            var receiverProject = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                NumberOfIncrementalIterations = incrementalIterations,
            };
            test.TestState.ExpectedDiagnostics.AddRange(locations.Select(x => VB.DiagnosticAt(x)));
            return test.RunAsync();
        }

        [Fact]
        public Task SystemNamespace_IsAdded_WhenMissing_CS()
        {
            string receiver = CS.Usings + @"
public class C
{
    public static void Consume(string text) { }
    public static void Consume(Roschar span) { }
}";
            string testCode = CS.WithBody(WithKey(@"C.Consume(foo.Substring(1))", 0) + ';', includeUsings: false);
            string fixedCode = CS.WithBody(@"C.Consume(foo.AsSpan(1));", includeUsings: true);

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiver },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, receiver }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task SystemNamespace_IsAdded_WhenNotIncludedGlobally_VB()
        {
            string receiver = CS.Usings + @"
public class C
{
    public static void Consume(string text) { }
    public static void Consume(Roschar span) { }
}";
            string testCode = VB.WithBody(WithKey(@"C.Consume(foo.Substring(1))", 0), includeImports: false);
            string fixedCode = VB.WithBody(@"C.Consume(foo.AsSpan(1))", includeImports: true);
            var project = new ProjectState("Receiver", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiver }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task SystemNamespace_IsNotAdded_WhenIncludedGlobally_VB()
        {
            string receiver = CS.Usings + @"
public class C
{
    public static void Consume(string text) { }
    public static void Consume(Roschar span) { }
}";
            string testCode = VB.WithBody(WithKey(@"C.Consume(foo.Substring(1))", 0), includeImports: false);
            string fixedCode = VB.WithBody(@"C.Consume(foo.AsSpan(1))", includeImports: false);
            var receiverProject = new ProjectState("Receiver", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiver }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            test.SolutionTransforms.Add((solution, id) =>
            {
                var project = solution.GetProject(id);
                if (project.Name == receiverProject.Name)
                    return solution;
                var options = (VisualBasicCompilationOptions)project.CompilationOptions;
                var globalSystemImport = GlobalImport.Parse(nameof(System));
                options = options.WithGlobalImports(globalSystemImport);
                return solution.WithProjectCompilationOptions(id, options);
            });
            return test.RunAsync();
        }

        //  No VB counterpart because imports must precede all declarations in VB.
        [Fact]
        public Task SystemNamespace_IsNotAdded_WhenImportedWithinNamespaceDeclaration_CS()
        {
            string format = @"
using Roschar = System.ReadOnlySpan<char>;

namespace Testopolis
{{
    using System;

    public class Body
    {{
        public void Consume(string text) {{ }}
        public void Consume(Roschar span) {{ }}
        public void Run(string foo)
        {{
            {0}
        }}
    }}
}}";
            string testCode = string.Format(CultureInfo.InvariantCulture, format, @"{|#0:Consume(foo.Substring(1))|};");
            string fixedCode = string.Format(CultureInfo.InvariantCulture, format, @"Consume(foo.AsSpan(1));");

            var test = new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("System")]
        [InlineData("System.Widgets")]
        public Task SystemNamespace_IsNotAdded_WhenViolationIsWithinSystemNamespace_CS(string namespaceDeclaration)
        {
            string format = @"
using Roschar = System.ReadOnlySpan<char>;

namespace " + namespaceDeclaration + @"
{{
    public class Body
    {{
        public void Consume(string text) {{ }}
        public void Consume(Roschar span) {{ }}
        public void Run(string foo)
        {{
            {0}
        }}
    }}
}}";
            string testCode = string.Format(CultureInfo.InvariantCulture, format, @"{|#0:Consume(foo.Substring(1))|};");
            string fixedCode = string.Format(CultureInfo.InvariantCulture, format, @"Consume(foo.AsSpan(1));");

            var test = new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("System")]
        [InlineData("System.Widgets")]
        public Task SystemNamespace_IsNotAdded_WhenViolationIsWithinSystemNamespace_VB(string namespaceDeclaration)
        {
            string helper = @"
using Roschar = System.ReadOnlySpan<char>;

public class Helper
{
    public void Consume(string text) { }
    public void Consume(Roschar span) { }
}";
            var project = new ProjectState("HelperProject", LanguageNames.CSharp, "helper", "cs")
            {
                Sources = { helper }
            };
            string format = @"
Namespace " + namespaceDeclaration + @"

    Public Class Body

        Private helper As Helper
        Public Sub Run(foo As String)

            {0}
        End Sub
    End Class
End Namespace";
            string testCode = string.Format(CultureInfo.InvariantCulture, format, @"{|#0:helper.Consume(foo.Substring(1))|}");
            string fixedCode = string.Format(CultureInfo.InvariantCulture, format, @"helper.Consume(foo.AsSpan(1))");

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleCandidateOverloads_SingleBestCandidate_CS
        {
            get
            {
                string members = @"
public void Consume(string a, string b, string c) { }
public void Consume(Roschar a, string b, string c) { }
public void Consume(string a, string b, Roschar c) { }
public void Consume(Roschar a, Roschar b, string c) { }";
                yield return new[]
                {
                    CS.WithBody(WithKey(@"Consume(foo.Substring(1), foo.Substring(2), foo.Substring(3))", 0) + ';', members),
                    CS.WithBody(@"Consume(foo.AsSpan(1), foo.AsSpan(2), foo.Substring(3));", members)
                };

                members = @"
public void Consume(int n, string b, string c) { }
public void Consume(double n, Roschar b, Roschar c) { }
public void Consume(int n, string b, Roschar c) { }";
                yield return new[]
                {
                    CS.WithBody(WithKey(@"Consume(7, foo.Substring(2), foo.Substring(3))", 0) + ';', members),
                    CS.WithBody(@"Consume(7, foo.Substring(2), foo.AsSpan(3));", members)
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleCandidateOverloads_SingleBestCandidate_CS))]
        public Task MultipleCandidateOverloads_SingleBestCandidate_ReportedAndFixed_CS(string testCode, string fixedCode)
        {
            var test = new VerifyCS.Test
            {
                TestCode = testCode,
                FixedCode = fixedCode,
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleCandidateOVerloads_SingleBestCandidate_VB
        {
            get
            {
                string receiver = CS.Usings + @"
public class R
{
    public static void Consume(string a, string b, string c) { }
    public static void Consume(Roschar a, string b, string c) { }
    public static void Consume(string a, string b, Roschar c) { }
    public static void Consume(Roschar a, Roschar b, string c) { }
}";
                yield return new[]
                {
                    receiver,
                    VB.WithBody(WithKey(@"R.Consume(foo.Substring(1), foo.Substring(2), foo.Substring(3))", 0)),
                    VB.WithBody(@"R.Consume(foo.AsSpan(1), foo.AsSpan(2), foo.Substring(3))")
                };

                receiver = CS.Usings + @"
public class R
{
    public static void Consume(int n, string b, string c) { }
    public static void Consume(double n, Roschar b, Roschar c) { }
    public static void Consume(int n, string b, Roschar c) { }
}";
                yield return new[]
                {
                    receiver,
                    VB.WithBody(WithKey(@"R.Consume(7, foo.Substring(2), foo.Substring(3))", 0)),
                    VB.WithBody(@"R.Consume(7, foo.Substring(2), foo.AsSpan(3))")
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleCandidateOVerloads_SingleBestCandidate_VB))]
        public Task MultipleCandidateOverloads_SingleBestCandidate_ReportedAndFixed_VB(string receiverClass, string testCode, string fixedCode)
        {
            var project = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleCandidateOverloads_Ambiguous_CS
        {
            get
            {
                string members = @"
public void Consume(string a, string b) { }
public void Consume(Roschar a, string b) { }
public void Consume(string a, Roschar b) { }";
                yield return new[]
                {
                    CS.WithBody(WithKey(@"Consume(foo.Substring(1), foo.Substring(2))", 0) + ';', members)
                };

                members = @"
public void Consume(string a, string b, string c) { }
public void Consume(string a, Roschar b, string c) { }
public void Consume(Roschar a, string b, Roschar c) { }
public void Consume(Roschar a, Roschar b, string c) { }
public void Consume(string a, Roschar b, Roschar c) { }";
                yield return new[]
                {
                    CS.WithBody(WithKey(@"Consume(foo.Substring(1), foo.Substring(2), foo.Substring(3))", 0) + ';', members)
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleCandidateOverloads_Ambiguous_CS))]
        public Task MultipleCandidateOverloads_Ambiguous_ReportedButNotFixed_CS(string testCode)
        {
            var test = new VerifyCS.Test
            {
                TestCode = testCode,
                ExpectedDiagnostics = { CS.DiagnosticAt(0) },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_MultipleCandidateOverloads_Ambiguous_VB
        {
            get
            {
                string receiver = CS.Usings + @"
public class R
{
    public static void Consume(string a, string b) { }
    public static void Consume(Roschar a, string b) { }
    public static void Consume(string a, Roschar b) { }
}";
                yield return new[]
                {
                    receiver,
                    VB.WithBody(WithKey(@"R.Consume(foo.Substring(1), foo.Substring(2))", 0))
                };

                receiver = CS.Usings + @"
public class R
{
    public static void Consume(string a, string b, string c) { }
    public static void Consume(string a, Roschar b, string c) { }
    public static void Consume(Roschar a, string b, Roschar c) { }
    public static void Consume(Roschar a, Roschar b, string c) { }
    public static void Consume(string a, Roschar b, Roschar c) { }
}";
                yield return new[]
                {
                    receiver,
                    VB.WithBody(WithKey(@"R.Consume(foo.Substring(1), foo.Substring(2), foo.Substring(3))", 0))
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_MultipleCandidateOverloads_Ambiguous_VB))]
        public Task MultipleCandidateOverloads_Ambiguous_ReportedButNotFixed_VB(string receiverClass, string testCode)
        {
            var project = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NoRoscharOverload_CS
        {
            get
            {
                string thing = CS.Usings + @"
public class Thing
{
    public static void Consume(string text) { }
}";

                yield return new[] { thing, @"Thing.Consume(foo.Substring(1))" };

                thing = CS.Usings + @"
public class Thing
{
    public static void Consume(string text) { }
    public static void Consume(Roschar span1, Roschar span2) { }
}";
                yield return new[] { thing, @"Thing.Consume(foo.Substring(1))" };

                thing = CS.Usings + @"
public class Thing
{
    public static void Consume(string text, int n) { }
    public static void Consume(int n, Roschar span) { }
}";
                yield return new[] { thing, @"Thing.Consume(foo.Substring(1), 17)" };
                yield return new[] { thing, @"Thing.Consume(n: 17, text: foo.Substring(1))" };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NoRoscharOverload_CS))]
        public Task NoRoscharOverload_NoDiagnostic_CS(string receiverClass, string testExpression)
        {
            string testCode = CS.WithBody(testExpression + ';');

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiverClass }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_NoRoscharOverload_VB
        {
            get
            {
                string thing = CS.Usings + @"
public class Thing
{
    public static void Consume(string text) { }
}";
                yield return new[] { thing, @"Thing.Consume(foo.Substring(1))" };

                thing = CS.Usings + @"
public class Thing
{
    public static void Consume(string text) { }
    public static void Consume(Roschar span1, Roschar span2) { }
}";
                yield return new[] { thing, @"Thing.Consume(foo.Substring(1))" };

                thing = CS.Usings + @"
public class Thing
{
    public static void Consume(string text, int n) { }
    public static void Consume(int n, Roschar span) { }
}";
                yield return new[] { thing, @"Thing.Consume(foo.Substring(1), 17)" };
                yield return new[] { thing, @"Thing.Consume(n:=17, text:=foo.Substring(1))" };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NoRoscharOverload_VB))]
        public Task NoRoscharOverload_NoDiagnostic_VB(string receiverClass, string testExpression)
        {
            string testCode = VB.WithBody(WithKey(testExpression, 0));
            var receiverProject = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { receiverProject.Name, receiverProject } },
                    AdditionalProjectReferences = { receiverProject.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_InvalidOverloads_CS
        {
            get
            {
                yield return new[]
                {
                    CS.Usings + @"
public class StaticToInstance
{
    public static void Consume(string text) { }
    public void Consume(Roschar span) { }
}",
                    @"StaticToInstance.Consume(foo.Substring(1));"
                };

                yield return new[]
                {
                    CS.Usings + @"
public class InstanceToStatic
{
    public void Consume(string text) { }
    public static void Consume(Roschar span) { }
}",
                    @"instance.Consume(foo.Substring(1));",
                    @"
partial class Body
{
    private InstanceToStatic instance = new InstanceToStatic();
}"
                };

                yield return new[]
                {
                    CS.Usings + @"
public class WrongReturnType
{
    public static string Make(string text) => throw null;
    public static int Make(Roschar span) => throw null;
}",
                    @"var _ = WrongReturnType.Make(foo.Substring(1));"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_InvalidOverloads_CS))]
        public Task InvalidOverloads_NoDiagnostic_CS(string receiverClass, string testStatements, string extraFields = "")
        {
            string testCode = CS.WithBody(testStatements);

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, receiverClass, extraFields }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        public static IEnumerable<object[]> Data_InvalidOverloads_VB
        {
            get
            {
                yield return new[]
                {
                    CS.Usings + @"
public class StaticToInstance
{
    public static void Consume(string text) { }
    public void Consume(Roschar span) { }
}",
                    @"StaticToInstance.Consume(foo.Substring(1))"
                };

                yield return new[]
                {
                    CS.Usings + @"
public class InstanceToStatic
{
    public void Consume(string text) { }
    public static void Consume(Roschar span) { }
}",
                    @"instance.Consume(foo.Substring(1))",
                    @"
Partial Class Body

    Private instance As InstanceToStatic = New InstanceToStatic()
End Class"
                };

                yield return new[]
                {
                    CS.Usings + @"
public class WrongReturnType
{
    public static string Make(string text) => throw null;
    public static int Make(Roschar span) => throw null;
}",
                    @"Dim m = WrongReturnType.Make(foo.Substring(1))"
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_InvalidOverloads_VB))]
        public Task InvalidOverloads_NoDiagnostic_VB(string receiverClass, string testStatements, string extraFields = "")
        {
            string testCode = VB.WithBody(testStatements);
            var project = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiverClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode, extraFields },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("parent.Private")]
        [InlineData("sibling.Private")]
        [InlineData("base.Private")]
        [InlineData("this.Private")]
        [InlineData("Private")]
        [InlineData("parent.ProtectedAndInternal")]
        [InlineData("sibling.ProtectedAndInternal")]
        [InlineData("base.ProtectedAndInternal")]
        [InlineData("this.ProtectedAndInternal")]
        [InlineData("ProtectedAndInternal")]
        [InlineData("parent.Internal")]
        [InlineData("sibling.Internal")]
        [InlineData("base.Internal")]
        [InlineData("this.Internal")]
        [InlineData("Internal")]
        [InlineData("parent.Protected")]
        [InlineData("parent.ProtectedOrInternal")]
        public Task Accessibility_ExternalBaseClass_WithoutDiagnostics_CS(string methodCallWithoutArgumentList)
        {
            string testCode = CS.Usings + @"
public class ExternalSubclass : External
{
    private string foo;
    private External parent;
    private ExternalSubclass sibling;
    public void NoDiagnostic()
    {
        " + methodCallWithoutArgumentList + @"(foo.Substring(1));
    }
}";
            var project = new ProjectState("ExternalProject", LanguageNames.CSharp, "external", "cs")
            {
                Sources = { CS.ExternalBaseClass }
            };

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("parent.Private")]
        [InlineData("sibling.Private")]
        [InlineData("MyBase.Private")]
        [InlineData("Me.Private")]
        [InlineData("[Private]")]
        [InlineData("parent.ProtectedAndInternal")]
        [InlineData("sibling.ProtectedAndInternal")]
        [InlineData("MyBase.ProtectedAndInternal")]
        [InlineData("Me.ProtectedAndInternal")]
        [InlineData("ProtectedAndInternal")]
        [InlineData("parent.Internal")]
        [InlineData("sibling.Internal")]
        [InlineData("MyBase.Internal")]
        [InlineData("Me.Internal")]
        [InlineData("parent.Protected")]
        [InlineData("parent.ProtectedOrInternal")]
        public Task Accessibility_ExternalBaseClass_WithoutDiagnostics_VB(string methodCallWithoutArgumentList)
        {
            string testCode = VB.Usings + @"
Public Class ExternalSubclass : Inherits External

    Private foo As String
    Private parent As External
    Private sibling As ExternalSubclass
    Public Sub NoDiagnostic()

        " + methodCallWithoutArgumentList + @"(foo.Substring(1))
    End Sub
End Class";
            var project = new ProjectState("ExternalProject", LanguageNames.CSharp, "external", "cs")
            {
                Sources = { CS.ExternalBaseClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("sibling.Protected")]
        [InlineData("base.Protected")]
        [InlineData("this.Protected")]
        [InlineData("Protected")]
        [InlineData("sibling.ProtectedOrInternal")]
        [InlineData("base.ProtectedOrInternal")]
        [InlineData("this.ProtectedOrInternal")]
        [InlineData("ProtectedOrInternal")]
        public Task Accessibility_ExternalBaseClass_WithDiagnostics_CS(string methodCallWithoutArgumentList)
        {
            string testCode = CS.Usings + @"
public class ExternalSubclass : External
{
    private string foo;
    private ExternalSubclass sibling;
    public void Diagnostic()
    {
        {|#0:" + methodCallWithoutArgumentList + @"(foo.Substring(1))|};
    }
}";
            string fixedCode = CS.Usings + @"
public class ExternalSubclass : External
{
    private string foo;
    private ExternalSubclass sibling;
    public void Diagnostic()
    {
        " + methodCallWithoutArgumentList + @"(foo.AsSpan(1));
    }
}";
            var project = new ProjectState("ExternalProject", LanguageNames.CSharp, "external", "cs")
            {
                Sources = { CS.ExternalBaseClass }
            };

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Theory]
        [InlineData("sibling.Protected")]
        [InlineData("MyBase.Protected")]
        [InlineData("Me.Protected")]
        [InlineData("[Protected]")]
        [InlineData("sibling.ProtectedOrInternal")]
        [InlineData("MyBase.ProtectedOrInternal")]
        [InlineData("Me.ProtectedOrInternal")]
        [InlineData("ProtectedOrInternal")]
        public Task Accessibility_ExternalBaseClass_WithDiagnostics_VB(string methodCallWithoutArgumentList)
        {
            string testCode = VB.Usings + @"
Public Class ExternalSubclass : Inherits External

    Private foo As String
    Private sibling As ExternalSubclass
    Private Sub Diagnostic()

        {|#0:" + methodCallWithoutArgumentList + @"(foo.Substring(1))|}
    End Sub
End Class";
            string fixedCode = VB.Usings + @"
Public Class ExternalSubclass : Inherits External

    Private foo As String
    Private sibling As ExternalSubclass
    Private Sub Diagnostic()

        " + methodCallWithoutArgumentList + @"(foo.AsSpan(1))
    End Sub
End Class";
            var project = new ProjectState("ExternalProject", LanguageNames.CSharp, "external", "cs")
            {
                Sources = { CS.ExternalBaseClass }
            };

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name },
                    ExpectedDiagnostics = { VB.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        //  No VB counterpart because VB doesn't support ref-like types in APIs.
        [Theory]
        [InlineData("parent.Private")]
        [InlineData("sibling.Private")]
        [InlineData("base.Private")]
        [InlineData("this.Private")]
        [InlineData("Private")]
        [InlineData("parent.Protected")]
        public Task Accessibility_InternalBaseClass_WithoutDiagnostics_CS(string methodCallWithoutArgumentList)
        {
            string testCode = CS.Usings + @"
public class InternalSubclass : Internal
{
    private string foo;
    private Internal parent;
    private InternalSubclass sibling;
    public void NoDiagnostic()
    {
        " + methodCallWithoutArgumentList + @"(foo.Substring(1));
    }
}";

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, CS.InternalBaseClass }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        //  No VB counterpart because VB doesn't support ref-like types in APIs.
        [Theory]
        [InlineData("sibling.Protected")]
        [InlineData("base.Protected")]
        [InlineData("this.Protected")]
        [InlineData("Protected")]
        public Task Accessibility_InternalBaseClass_WithDiagnostics_CS(string methodCallWithoutArgumentList)
        {
            string testCode = CS.Usings + @"
public class InternalSubclass : Internal
{
    private string foo;
    private InternalSubclass sibling;
    public void Diagnostic()
    {
        {|#0:" + methodCallWithoutArgumentList + @"(foo.Substring(1))|};
    }
}";
            string fixedCode = CS.Usings + @"
public class InternalSubclass : Internal
{
    private string foo;
    private InternalSubclass sibling;
    public void Diagnostic()
    {
        " + methodCallWithoutArgumentList + @"(foo.AsSpan(1));
    }
}";

            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { testCode, CS.InternalBaseClass },
                    ExpectedDiagnostics = { CS.DiagnosticAt(0) }
                },
                FixedState =
                {
                    Sources = { fixedCode, CS.InternalBaseClass }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ConditionalSubstringAccess_NoDiagnostic_CS()
        {
            string testCode = CS.Usings + @"
public class Body
{
    public void Consume(string text) { }
    public void Consume(Roschar span) { }
    public void Run(string foo)
    {
        Consume(foo?.Substring(1));
    }
}";

            var test = new VerifyCS.Test
            {
                TestCode = testCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        [Fact]
        public Task ConditionalSubstringAccess_NoDiagnostic_VB()
        {
            string receiver = CS.Usings + @"
public class Receiver
{
    public void Consume(string text) { }
    public void Consume(Roschar span) { }
}";
            var project = new ProjectState("ReceiverProject", LanguageNames.CSharp, "receiver", "cs")
            {
                Sources = { receiver }
            };
            string testCode = VB.WithBody(
                @"
Dim receiver = New Receiver()
receiver.Consume(foo?.Substring(1))");

            var test = new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { testCode },
                    AdditionalProjects = { { project.Name, project } },
                    AdditionalProjectReferences = { project.Name }
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };
            return test.RunAsync();
        }

        #region Helpers
        private static class CS
        {
            public const string Usings = @"
using System;
using Roschar = System.ReadOnlySpan<char>;";
            public const string ExternalBaseClass = Usings + @"
public class External
{
    public void ProtectedOrInternal(string text) { }
    protected internal void ProtectedOrInternal(Roschar span) { }

    public void Protected(string text) { }
    protected void Protected(Roschar span) { }

    public void Internal(string text) { }
    internal void Internal(Roschar span) { }

    public void ProtectedAndInternal(string text) { }
    private protected void ProtectedAndInternal(Roschar span) { }

    public void Private(string text) { }
    private void Private(Roschar span) { }
}";
            public const string InternalBaseClass = Usings + @"
public class Internal
{
    public void Private(string text) { }
    private void Private(Roschar span) { }

    public void Protected(string text) { }
    protected void Protected(Roschar span) { }
}";

            public static string WithBody(string statements, bool includeUsings = true)
            {
                string indentedStatements = IndentLines(statements, "        ");
                string usings = includeUsings ? $"{Environment.NewLine}using System;{Environment.NewLine}" : string.Empty;

                return $@"
{usings}
public partial class Body
{{
    private void Run(string foo)
    {{
{indentedStatements}
    }}
}}";
            }
            public static string WithBody(string statements, string members)
            {
                return Usings + $@"
public partial class Body
{{
{IndentLines(members, "    ")}
    private void Run(string foo)
    {{
{IndentLines(statements, "        ")}
    }}
}}";
            }

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyCS.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static class VB
        {
            public const string Usings = @"
Imports System";

            public static string WithBody(string statements, bool includeImports = true)
            {
                const string indent = "        ";
                string indentedStatements = indent + statements.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent, StringComparison.Ordinal);
                string imports = includeImports ? $"{Environment.NewLine}Imports System{Environment.NewLine}" : string.Empty;

                return $@"
{imports}
Partial Public Class Body

    Private Sub Run(foo As String)

{indentedStatements}
    End Sub
End Class";
            }

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyVB.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static string WithKey(string text, int markupKey) => $"{{|#{markupKey}:{text}|}}";

        private static string IndentLines(string lines, string indent) => indent + lines.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent, StringComparison.Ordinal);
        private static DiagnosticDescriptor Rule => PreferAsSpanOverSubstring.Rule;
        #endregion
    }
}
