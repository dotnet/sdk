// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        #region Reports Diagnostic
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
                    2, 1, 1
                };
                yield return new object[]
                {
                    receiver,
                    @"{|#0:C.Consume({|#1:C.Fwd(foo.Substring(1), foo.Substring(2))|}.Substring(3), foo.Substring(4))|}",
                    @"C.Consume(C.Fwd(foo.AsSpan(1), foo.AsSpan(2)).AsSpan(3), foo.AsSpan(4))",
                    new[] { 0, 1 },
                    2, 1, 1
                };
                yield return new object[]
                {
                    receiver,
                    @"{|#0:C.Consume({|#1:C.Fwd(foo.Substring(1), {|#2:C.Fwd(foo.Substring(2))|}.Substring(3))|}.Substring(4))|}",
                    @"C.Consume(C.Fwd(foo.AsSpan(1), C.Fwd(foo.AsSpan(2)).AsSpan(3)).AsSpan(4))",
                    new[] { 0, 1, 2 },
                    3, 1, 1
                };
            }
        }

        [Theory]
        [MemberData(nameof(Data_NestedViolations))]
        public Task NestedViolations_AreAllReportedAndFixed_CS(
            string receiverClass, string testExpression, string fixedExpression, int[] locations,
            int? incrementalIterations = null, int? fixAllInDocumentIterations = null, int? fixAllIterations = null)
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
                NumberOfFixAllInDocumentIterations = fixAllInDocumentIterations,
                NumberOfFixAllIterations = fixAllIterations
            };
            test.TestState.ExpectedDiagnostics.AddRange(locations.Select(x => CS.DiagnosticAt(x)));
            return test.RunAsync();
        }

        [Theory]
        [MemberData(nameof(Data_NestedViolations))]
        public Task NestedViolations_AreAllReportedAndFixed_VB(
            string receiverClass, string testExpression, string fixedExpression, int[] locations,
            int? incrementalIterations = null, int? fixAllInDocumentIterations = null, int? fixAllIterations = null)
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
                NumberOfFixAllInDocumentIterations = fixAllInDocumentIterations,
                NumberOfFixAllIterations = fixAllIterations
            };
            test.TestState.ExpectedDiagnostics.AddRange(locations.Select(x => VB.DiagnosticAt(x)));
            return test.RunAsync();
        }

        [Fact]
        public Task MissingUsings_AreAdded_CS()
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
        public Task MissingUsings_AreAdded_WhenNotIncludedGlobally_VB()
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
        public Task MissingUsings_AreNotAdded_WhenIncludedGlobally_VB()
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
        #endregion

        #region No Diagnostic
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
        #endregion

        #region Helpers
        private static class CS
        {
            public const string Usings = @"
using System;
using Roschar = System.ReadOnlySpan<char>;";

            public static string WithBody(string statements, bool includeUsings = true)
            {
                const string indent = "        ";
                string indentedStatements = indent + statements.TrimStart().Replace(Environment.NewLine, Environment.NewLine + indent, StringComparison.Ordinal);
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

            public static DiagnosticResult DiagnosticAt(int markupKey) => VerifyCS.Diagnostic(Rule).WithLocation(markupKey);
        }

        private static class VB
        {
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
        private static DiagnosticDescriptor Rule => PreferAsSpanOverSubstring.Rule;

        #endregion
    }
}
