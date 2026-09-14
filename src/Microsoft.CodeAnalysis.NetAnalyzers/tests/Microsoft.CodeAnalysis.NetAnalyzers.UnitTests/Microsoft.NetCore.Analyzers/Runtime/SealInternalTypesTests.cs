// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SealInternalTypes,
    Microsoft.NetCore.Analyzers.Runtime.SealInternalTypesFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.SealInternalTypes,
    Microsoft.NetCore.Analyzers.Runtime.SealInternalTypesFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class SealInternalTypesTests
    {
        // NOTE: 'SealInternalTypes' analyzer reports a compilation end diagnostic.
        //       Code fixes are not yet supported for compilation end diagnostics,
        //       hence 'SealInternalTypesFixer' is a no-op right now.
        //       However, we have still implemented this fixer so that if code fix support
        //       gets added for compilation end diagnostics in future, it should automatically light up.
        //       All tests in this file are marked with 'CodeFixTestBehaviors.SkipLocalDiagnosticCheck'
        //       as compilation end diagnostics are non-local diagnostics.

        #region Diagnostic
        [Theory]
        [InlineData("internal ")]
        [InlineData("")]
        public async Task TopLevelInternalClass_Diagnostic_CS(string accessModifier)
        {
            string source = $"{accessModifier}class {{|#0:C|}} {{ }}";
            string fixedSource = $"{accessModifier}sealed class C {{ }}";

            await new VerifyCS.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("Friend ")]
        [InlineData("")]
        public async Task TopLevelInternalClass_Diagnostic_VB(string accessModifier)
        {
            string source = $@"
{accessModifier}Class {{|#0:C|}}
End Class";
            string fixedSource = $@"
{accessModifier}NotInheritable Class C
End Class";

            await new VerifyVB.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Fact]
        public async Task NonEmptyInternalClass_Diagnostic_CS()
        {
            string source = @"
internal class {|#0:C|}
{
    private int _i;
}";
            string fixedSource = @"
internal sealed class C
{
    private int _i;
}";

            await new VerifyCS.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("internal ")]
        [InlineData("")]
        public async Task InternalClassInNamespace_Diagnostic_CS(string accessModifier)
        {
            string source = $@"
namespace N
{{
    {accessModifier}class {{|#0:C|}} {{ }}
}}";
            string fixedSource = $@"
namespace N
{{
    {accessModifier}sealed class C {{ }}
}}";

            await new VerifyCS.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("Friend ")]
        [InlineData("")]
        public async Task InternalClassInNamespace_Diagnostic_VB(string accessModifier)
        {
            string source = $@"
Namespace N
    {accessModifier}Class {{|#0:C|}}
    End Class
End Namespace";
            string fixedSource = $@"
Namespace N
    {accessModifier}NotInheritable Class C
    End Class
End Namespace";

            await new VerifyVB.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("public", "internal")]
        [InlineData("public", "private")]
        [InlineData("public", "private protected")]
        [InlineData("internal", "public")]
        [InlineData("internal", "protected internal")]
        public async Task NestedOneDeep_NotExternallyVisible_Diagnostic_CS(string outerModifiers, string innerModifiers)
        {
            string source = $@"
{outerModifiers} sealed class Outer
{{
    {innerModifiers} class {{|#0:C|}} {{ }}
}}";
            string fixedSource = $@"
{outerModifiers} sealed class Outer
{{
    {innerModifiers} sealed class C {{ }}
}}";

            await new VerifyCS.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("Public", "Friend")]
        [InlineData("Public", "Private")]
        [InlineData("Public", "Private Protected")]
        [InlineData("Friend", "Public")]
        [InlineData("Friend", "Friend Protected")]
        public async Task NestedOneDeep_NotExternallyVisible_Diagnostic_VB(string outerModifiers, string innerModifiers)
        {
            string source = $@"
{outerModifiers} NotInheritable Class Outer
    {innerModifiers} Class {{|#0:C|}}
    End Class
End Class";
            string fixedSource = $@"
{outerModifiers} NotInheritable Class Outer
    {innerModifiers} NotInheritable Class C
    End Class
End Class";

            await new VerifyVB.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("public", "public", "internal")]
        [InlineData("internal", "internal protected", "public")]
        [InlineData("public", "private protected", "public")]
        public async Task NestedTwoDeep_NotExternallyVisible_Diagnostic_CS(string outerModifiers, string middleModifiers, string innerModifiers)
        {
            string source = $@"
{outerModifiers} sealed class Outer
{{
    {middleModifiers} sealed class Middle
    {{
        {innerModifiers} class {{|#0:C|}} {{ }}
    }}
}}";
            string fixedSource = $@"
{outerModifiers} sealed class Outer
{{
    {middleModifiers} sealed class Middle
    {{
        {innerModifiers} sealed class {{|#0:C|}} {{ }}
    }}
}}";

            await new VerifyCS.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyCS.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [InlineData("Public", "Public", "Friend")]
        [InlineData("Friend", "Friend Protected", "Public")]
        [InlineData("Public", "Private Protected", "Public")]
        public async Task NestedTwoDeep_NotExternallyVisible_Diagnostic_VB(string outerModifiers, string middleModifiers, string innerModifiers)
        {
            string source = $@"
{outerModifiers} NotInheritable Class Outer
    {middleModifiers} NotInheritable Class Middle
        {innerModifiers} Class {{|#0:C|}}
        End Class
    End Class
End Class";
            string fixedSource = $@"
{outerModifiers} NotInheritable Class Outer
    {middleModifiers} NotInheritable Class Middle
        {innerModifiers} NotInheritable Class C
        End Class
    End Class
End Class";

            await new VerifyVB.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestCode = source,
                ExpectedDiagnostics =
                {
                    VerifyVB.Diagnostic(Rule).WithArguments("C").WithLocation(0),
                },
                FixedCode = fixedSource,
            }.RunAsync();
        }

        [Theory]
        [CombinatorialData]
        public async Task InternalsVisibleTo_Diagnostic_WhenOptionsDemandIt(bool ignoreInternalsVisibleTo)
        {
            string source = @"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""TestProject"")]
                              internal class {|#0:C|} { }";

            var test = new VerifyCS.Test
            {
                TestCode = source,
                TestState =
                {
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
dotnet_code_quality.CA1852.ignore_internalsvisibleto = {ignoreInternalsVisibleTo}
") }
                }
            };

            if (ignoreInternalsVisibleTo)
            {
                test.ExpectedDiagnostics.Add(
                    VerifyCS.Diagnostic(Rule)
                        .WithLocation(0)
                        .WithArguments("C"));
            }

            await test.RunAsync();
        }

        #endregion

        #region No Diagnostic
        [Fact, WorkItem(6141, "https://github.com/dotnet/roslyn-analyzers/issues/6141")]
        public Task TopLevelStatementsProgram()
        {
            return new VerifyCS.Test()
            {
                TestState =
                {
                    Sources = { "System.Console.WriteLine();" },
                    OutputKind = OutputKind.ConsoleApplication,
                },
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
            }.RunAsync();
        }

        [Fact]
        public Task PublicClassType_NoDiagnostic_CS()
        {
            string source = $"public class C {{ protected class P {{ }} }}";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task AlreadySealedType_NoDiagnostic_CS()
        {
            string source = $"internal sealed class C {{ }}";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task InternalsVisibleTo_NoDiagnostic()
        {
            string source = @"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""TestProject"")]
                              internal class C { }";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task ComImportAttributedType_NoDiagnostic_CS()
        {
            string source = $"[System.Runtime.InteropServices.ComImport] [System.Runtime.InteropServices.Guid(\"E8D59775-E821-4D6C-B63D-BB0D969361DA\")] internal class C {{ }}";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("interface I { }")]
        [InlineData("struct S { }")]
        [InlineData("enum E { None }")]
        [InlineData("delegate void D();")]
        [InlineData("static class C { }")]
        public Task NonClassType_NoDiagnostic_CS(string declaration)
        {
            string source = $"internal {declaration}";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("Interface I", "End Interface")]
        [InlineData("Structure S", "End Structure")]
        [InlineData(@"Enum E
    None", "End Enum")]
        [InlineData("Delegate Sub D()", "")]
        [InlineData("Module M", "End Module")]
        public Task NonClassType_NoDiagnostic_VB(string declaration, string endDeclaration)
        {
            string source = $@"
Friend {declaration}
{endDeclaration}";

            return VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task ClassWithDerivedType_NoDiagnostic_CS()
        {
            string source = @"
internal class B { }
internal sealed class D : B { }";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task ClassWithDerivedType_NoDiagnostic_VB()
        {
            string source = @"
Friend Class B
End Class
Friend NotInheritable Class D : Inherits B
End Class";

            return VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task AbstractClass_NoDiagnostic_CS()
        {
            string source = "internal abstract class C { }";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task AbstractClass_NoDiagnostic_VB()
        {
            string source = @"
Friend MustInherit Class C
End Class";

            return VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("B<T> { }", "D : B<int> { }")]
        [InlineData("B<T> { }", "D<T> : B<T> { }")]
        [InlineData("B<T, U> { }", "D<T> : B<T, int> { }")]
        public Task GenericClass_WithSubclass_NoDiagnostic_CS(string baseClass, string derivedClass)
        {
            string source = $@"
internal class {baseClass}
internal sealed class {derivedClass}";

            return VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Theory]
        [InlineData("B(Of T)", "D : Inherits B(Of Integer)")]
        [InlineData("B(Of T)", "D(Of T) : Inherits B(Of T)")]
        [InlineData("B(Of T, U)", "D(Of T) : Inherits B(Of T, Integer)")]
        public Task GenericClass_WithSubclass_NoDiagnostic_VB(string baseClass, string derivedClass)
        {
            string source = $@"
Friend Class {baseClass}
End Class

Friend NotInheritable Class {derivedClass}
End Class";

            return VerifyVB.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public Task PartialClass_ReportedAndFixedAtAllLocations_CS()
        {
            var test = new VerifyCS.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestState =
                {
                    Sources =
                    {
                        @"internal class Base { }",
                        @"internal partial class {|#0:Derived|} : Base { }",
                        @"internal partial class {|#1:Derived|} : Base { }"
                    },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(Rule).WithArguments("Derived").WithLocation(0).WithLocation(1)
                    }
                },
                FixedState =
                {
                    Sources =
                    {
                        @"internal class Base { }",
                        @"internal sealed partial class Derived : Base { }",
                        @"internal sealed partial class Derived : Base { }"
                    }
                }
            };
            return test.RunAsync();
        }

        [Fact(Skip = "Changes are being applied to .g.cs file")]
        public Task PartialClass_OneGenerated_ReportedAndFixedAtAllNonGeneratedLocations_CS()
        {
            var test = new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        ("File1.cs", @"internal class Base { }"),
                        ("File2.cs", @"internal partial class {|#0:Derived|} : Base { }"),
                        ("File3.g.cs", @"internal partial class {|#1:Derived|} : Base { }")
                    },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(Rule).WithArguments("Derived").WithLocation(0).WithLocation(1)
                    }
                },
                FixedState =
                {
                    Sources =
                    {
                        ("File1.cs", @"internal class Base { }"),
                        ("File2.cs", @"internal sealed partial class {|#0:Derived|} : Base { }"),
                        ("File3.g.cs", @"internal partial class {|#1:Derived|} : Base { }")
                    }
                }
            };
            return test.RunAsync();
        }

        [Fact]
        public Task PartialClass_ReportedAndFixedAtAllLocations_VB()
        {
            var test = new VerifyVB.Test
            {
                CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
                TestState =
                {
                    Sources =
                    {
                        @"
Friend Class Base
End Class", @"
Partial Friend Class {|#0:Derived|} : Inherits Base
End Class", @"
Partial Friend Class {|#1:Derived|} : Inherits Base
End Class"
                    },
                    ExpectedDiagnostics =
                    {
                        VerifyVB.Diagnostic(Rule).WithArguments("Derived").WithLocation(0).WithLocation(1)
                    }
                },
                FixedState =
                {
                    Sources =
                    {
                        @"
Friend Class Base
End Class", @"
Partial Friend NotInheritable Class Derived : Inherits Base
End Class", @"
Partial Friend NotInheritable Class Derived : Inherits Base
End Class"
                    }
                }
            };
            return test.RunAsync();
        }
        #endregion

        private static DiagnosticDescriptor Rule => SealInternalTypes.Rule;
    }
}
