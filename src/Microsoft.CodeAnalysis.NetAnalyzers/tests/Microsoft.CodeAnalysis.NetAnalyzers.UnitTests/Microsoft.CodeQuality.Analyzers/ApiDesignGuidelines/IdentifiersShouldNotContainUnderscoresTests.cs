
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotContainUnderscoresAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldNotContainUnderscoresFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotContainUnderscoresAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldNotContainUnderscoresFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class IdentifiersShouldNotContainUnderscoresTests
    {
        #region CSharp Tests
        [Fact]
        public async Task CA1707_ForAssembly_CSharp() // TODO: How to test the code fixer for this?
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class DoesNotMatter
{
}
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                        solution.WithProjectAssemblyName(projectId, "AssemblyNameHasUnderScore_")
                },
                ExpectedDiagnostics =
                {
                    GetCA1707CSharpResultAt(line: 2, column: 1, symbolKind: SymbolKind.Assembly, identifierNames: "AssemblyNameHasUnderScore_")
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1707_ForAssembly_NoDiagnostics_CSharp()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
public class DoesNotMatter
{
}
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                        solution.WithProjectAssemblyName(projectId, "AssemblyNameHasNoUnderScore")
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1707_ForNamespace_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
namespace OuterNamespace
{
    namespace {|#0:HasUnderScore_|}
    {
        public class DoesNotMatter
        {
        }
    }
}

namespace HasNoUnderScore
{
    public class DoesNotMatter
    {
    }
}",
            VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.NamespaceRule).WithLocation(0).WithArguments("OuterNamespace.HasUnderScore_"), @"
namespace OuterNamespace
{
    namespace HasUnderScore
    {
        public class DoesNotMatter
        {
        }
    }
}

namespace HasNoUnderScore
{
    public class DoesNotMatter
    {
    }
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1707_ForTypes_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class OuterType
{
    public class {|#0:UnderScoreInName_|}
    {
    }

    private class UnderScoreInNameButPrivate_
    {
    }

    internal class UnderScoreInNameButInternal_
    {
    }
}

internal class OuterType2
{
    public class UnderScoreInNameButNotExternallyVisible_
    {
    }
}
",
            VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.TypeRule).WithLocation(0).WithArguments("OuterType.UnderScoreInName_"), @"
public class OuterType
{
    public class UnderScoreInName
    {
    }

    private class UnderScoreInNameButPrivate_
    {
    }

    internal class UnderScoreInNameButInternal_
    {
    }
}

internal class OuterType2
{
    public class UnderScoreInNameButNotExternallyVisible_
    {
    }
}
");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1707_ForFields_CSharp()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    { @"
public class DoesNotMatter
{
        public const int {|#0:ConstField_|} = 5;
        public static readonly int {|#1:StaticReadOnlyField_|} = 5;

        // No diagnostics for the below
        private string InstanceField_;
        private static string StaticField_;
        public string _field;
        protected string Another_field;
}

public enum DoesNotMatterEnum
{
    {|#2:_EnumWithUnderscore|},
    {|#3:_|}
}

public class C
{
    internal class C2
    {
        public const int ConstField_ = 5;
    }
}
",
                    },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.ConstField_"),
                        VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.StaticReadOnlyField_"),
                        VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("DoesNotMatterEnum._EnumWithUnderscore"),
                        VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("DoesNotMatterEnum._"),
                    }
                },
                FixedState =
                {
                    Sources =
                    {
                         @"
public class DoesNotMatter
{
        public const int ConstField = 5;
        public static readonly int StaticReadOnlyField = 5;

        // No diagnostics for the below
        private string InstanceField_;
        private static string StaticField_;
        public string _field;
        protected string Another_field;
}

public enum DoesNotMatterEnum
{
    EnumWithUnderscore,
    {|#0:_|}
}

public class C
{
    internal class C2
    {
        public const int ConstField_ = 5;
    }
}
",
                    },
                    ExpectedDiagnostics =
                    {
                        VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatterEnum._"),
                    },
                },
            }.RunAsync();
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1707_ForMethods_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class DoesNotMatter
{
    public void {|#0:PublicM1_|}() { }
    private void PrivateM2_() { } // No diagnostic
    internal void InternalM3_() { } // No diagnostic
    protected void {|#1:ProtectedM4_|}() { }
}

public interface I1
{
    void {|#2:M_|}();
}

public class ImplementI1 : I1
{
    public void M_() { } // No diagnostic
    public virtual void {|#3:M2_|}() { }
}

public class Derives : ImplementI1
{
    public override void M2_() { } // No diagnostic
}

internal class C
{
    public class DoesNotMatter2
    {
        public void PublicM1_() { } // No diagnostic
        protected void ProtectedM4_() { } // No diagnostic
    }
}", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.PublicM1_()"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedM4_()"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("I1.M_()"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("ImplementI1.M2_()")
}, @"
public class DoesNotMatter
{
    public void PublicM1() { }
    private void PrivateM2_() { } // No diagnostic
    internal void InternalM3_() { } // No diagnostic
    protected void ProtectedM4() { }
}

public interface I1
{
    void M();
}

public class ImplementI1 : I1
{
    public void M() { } // No diagnostic
    public virtual void M2() { }
}

public class Derives : ImplementI1
{
    public override void M2() { } // No diagnostic
}

internal class C
{
    public class DoesNotMatter2
    {
        public void PublicM1_() { } // No diagnostic
        protected void ProtectedM4_() { } // No diagnostic
    }
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1707_ForProperties_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class DoesNotMatter
{
    public int {|#0:PublicP1_|} { get; set; }
    private int PrivateP2_ { get; set; } // No diagnostic
    internal int InternalP3_ { get; set; } // No diagnostic
    protected int {|#1:ProtectedP4_|} { get; set; }
}

public interface I1
{
    int {|#2:P_|} { get; set; }
}

public class ImplementI1 : I1
{
    public int P_ { get; set; } // No diagnostic
    public virtual int {|#3:P2_|} { get; set; }
}

public class Derives : ImplementI1
{
    public override int P2_ { get; set; } // No diagnostic
}

internal class C
{
    public class DoesNotMatter2
    {
        public int PublicP1_ { get; set; }// No diagnostic
        protected int ProtectedP4_ { get; set; } // No diagnostic
    }
}", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.PublicP1_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedP4_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("I1.P_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("ImplementI1.P2_")
}, @"
public class DoesNotMatter
{
    public int PublicP1 { get; set; }
    private int PrivateP2_ { get; set; } // No diagnostic
    internal int InternalP3_ { get; set; } // No diagnostic
    protected int ProtectedP4 { get; set; }
}

public interface I1
{
    int P { get; set; }
}

public class ImplementI1 : I1
{
    public int P { get; set; } // No diagnostic
    public virtual int P2 { get; set; }
}

public class Derives : ImplementI1
{
    public override int P2 { get; set; } // No diagnostic
}

internal class C
{
    public class DoesNotMatter2
    {
        public int PublicP1_ { get; set; }// No diagnostic
        protected int ProtectedP4_ { get; set; } // No diagnostic
    }
}");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task CA1707_ForEvents_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

public class DoesNotMatter
{
    public event EventHandler {|#0:PublicE1_|};
    private event EventHandler PrivateE2_; // No diagnostic
    internal event EventHandler InternalE3_; // No diagnostic
    protected event EventHandler {|#1:ProtectedE4_|};
}

public interface I1
{
    event EventHandler {|#2:E_|};
}

public class ImplementI1 : I1
{
    public event EventHandler E_;// No diagnostic
    public virtual event EventHandler {|#3:E2_|};
}

public class Derives : ImplementI1
{
    public override event EventHandler E2_; // No diagnostic
}

internal class C
{
    public class DoesNotMatter
    {
        public event EventHandler PublicE1_; // No diagnostic
        protected event EventHandler ProtectedE4_; // No diagnostic
    }
}", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.PublicE1_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedE4_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("I1.E_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("ImplementI1.E2_")
}, @"
using System;

public class DoesNotMatter
{
    public event EventHandler PublicE1;
    private event EventHandler PrivateE2_; // No diagnostic
    internal event EventHandler InternalE3_; // No diagnostic
    protected event EventHandler ProtectedE4;
}

public interface I1
{
    event EventHandler E;
}

public class ImplementI1 : I1
{
    public event EventHandler E;// No diagnostic
    public virtual event EventHandler E2;
}

public class Derives : ImplementI1
{
    public override event EventHandler E2; // No diagnostic
}

internal class C
{
    public class DoesNotMatter
    {
        public event EventHandler PublicE1_; // No diagnostic
        protected event EventHandler ProtectedE4_; // No diagnostic
    }
}");
        }

        [Fact]
        public async Task CA1707_ForDelegates_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public delegate void Dele(int {|#0:intPublic_|}, string {|#1:stringPublic_|});
internal delegate void Dele2(int intInternal_, string stringInternal_); // No diagnostics
public delegate T Del<T>(int {|#2:t_|});
", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule).WithLocation(0).WithArguments("Dele", "intPublic_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule).WithLocation(1).WithArguments("Dele", "stringPublic_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule).WithLocation(2).WithArguments("Del<T>", "t_")
}, @"
public delegate void Dele(int intPublic, string stringPublic);
internal delegate void Dele2(int intInternal_, string stringInternal_); // No diagnostics
public delegate T Del<T>(int t);
");
        }

        [Fact]
        public async Task CA1707_ForMemberparameters_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class DoesNotMatter
{
    public void PublicM1(int {|#0:int_|}) { }
    private void PrivateM2(int int_) { } // No diagnostic
    internal void InternalM3(int int_) { } // No diagnostic
    protected void ProtectedM4(int {|#1:int_|}) { }
}

public interface I
{
    void M(int {|#2:int_|});
}

public class implementI : I
{
    public void M(int int_) // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
    }
}

public abstract class Base
{
    public virtual void M1(int {|#3:int_|})
    {
    }

    public abstract void M2(int {|#4:int_|});
}

public class Der : Base
{
    public override void M2(int int_) // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        throw new System.NotImplementedException();
    }

    public override void M1(int int_) // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        base.M1(int_);
    }
}", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(0).WithArguments("DoesNotMatter.PublicM1(int)", "int_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedM4(int)", "int_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(2).WithArguments("I.M(int)", "int_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(3).WithArguments("Base.M1(int)", "int_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(4).WithArguments("Base.M2(int)", "int_")
}, @"
public class DoesNotMatter
{
    public void PublicM1(int @int) { }
    private void PrivateM2(int int_) { } // No diagnostic
    internal void InternalM3(int int_) { } // No diagnostic
    protected void ProtectedM4(int @int) { }
}

public interface I
{
    void M(int @int);
}

public class implementI : I
{
    public void M(int int_) // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
    }
}

public abstract class Base
{
    public virtual void M1(int @int)
    {
    }

    public abstract void M2(int @int);
}

public class Der : Base
{
    public override void M2(int int_) // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        throw new System.NotImplementedException();
    }

    public override void M1(int int_) // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        base.M1(int_);
    }
}");
        }

        [Fact]
        public async Task CA1707_ForTypeTypeParameters_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class DoesNotMatter<{|#0:T_|}>
{
}

class NoDiag<U_>
{
}", VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.TypeTypeParameterRule).WithLocation(0).WithArguments("DoesNotMatter<T_>", "T_"), @"
public class DoesNotMatter<T>
{
}

class NoDiag<U_>
{
}");
        }

        [Fact]
        public async Task CA1707_ForMemberTypeParameters_CSharp()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
public class DoesNotMatter22
{
    public void PublicM1<{|#0:T1_|}>() { }
    private void PrivateM2<U_>() { } // No diagnostic
    internal void InternalM3<W_>() { } // No diagnostic
    protected void ProtectedM4<{|#1:D_|}>() { }
}

public interface I
{
    void M<{|#2:T_|}>();
}

public class implementI : I
{
    public void M<U_>()
    {
        throw new System.NotImplementedException();
    }
}

public abstract class Base
{
    public virtual void M1<{|#3:T_|}>()
    {
    }

    public abstract void M2<{|#4:U_|}>();
}

public class Der : Base
{
    public override void M2<U_>() // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        throw new System.NotImplementedException();
    }

    public override void M1<T_>() // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        base.M1<T_>();
    }
}", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(0).WithArguments("DoesNotMatter22.PublicM1<T1_>()", "T1_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(1).WithArguments("DoesNotMatter22.ProtectedM4<D_>()", "D_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(2).WithArguments("I.M<T_>()", "T_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(3).WithArguments("Base.M1<T_>()", "T_"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(4).WithArguments("Base.M2<U_>()", "U_")
}, @"
public class DoesNotMatter22
{
    public void PublicM1<T1>() { }
    private void PrivateM2<U_>() { } // No diagnostic
    internal void InternalM3<W_>() { } // No diagnostic
    protected void ProtectedM4<D>() { }
}

public interface I
{
    void M<T>();
}

public class implementI : I
{
    public void M<U_>()
    {
        throw new System.NotImplementedException();
    }
}

public abstract class Base
{
    public virtual void M1<T>()
    {
    }

    public abstract void M2<U>();
}

public class Der : Base
{
    public override void M2<U_>() // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        throw new System.NotImplementedException();
    }

    public override void M1<T_>() // This is not renamed due to https://github.com/dotnet/roslyn/issues/46663
    {
        base.M1<T_>();
    }
}");
        }

        [Fact, WorkItem(947, "https://github.com/dotnet/roslyn-analyzers/issues/947")]
        public async Task CA1707_ForOperators_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public struct S
{
    public static bool operator ==(S left, S right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(S left, S right)
    {
        return !(left == right);
    }
}
");
        }

        [Fact, WorkItem(1319, "https://github.com/dotnet/roslyn-analyzers/issues/1319")]
        public async Task CA1707_CustomOperator_CSharp()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Span
{
    public static implicit operator Span(string text) => new Span(text);
    public static explicit operator string(Span span) => span.GetText();
    private string _text;

    public Span(string text)
    {
        this._text = text;
    }

    public string GetText() => _text;
}
");
        }

        [Fact]
        public async Task CA1707_CSharp_DiscardSymbolParameter_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public static class MyHelper
{
    public static int GetSomething(this string _) => 42;

    public static void SomeMethod()
    {
        SomeOtherMethod(out _);
    }

    public static void SomeOtherMethod(out int p)
    {
        p = 42;
    }
}");
        }

        [Fact]
        public async Task CA1707_CSharp_DiscardSymbolTuple_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
    public SomeClass()
    {
        var (_, d) = GetSomething();
    }

    private static (string, double) GetSomething() => ("""", 0);
}");
        }

        [Fact]
        public async Task CA1707_CSharp_DiscardSymbolPatternMatching_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
    public SomeClass(object o)
    {
        switch (o)
        {
            case object _:
                break;
        }
    }
}");
        }

        [Fact]
        public async Task CA1707_CSharp_StandaloneDiscardSymbol_NoDiagnostic()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class SomeClass
{
    public SomeClass(object o)
    {
        _ = GetSomething();
    }

    public int GetSomething() => 42;
}");
        }

        [Fact, WorkItem(3121, "https://github.com/dotnet/roslyn-analyzers/issues/3121")]
        public async Task CA1707_CSharp_GlobalAsaxSpecialMethods()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;

namespace System.Web
{
    public class HttpApplication {}
}

public class ValidContext : System.Web.HttpApplication
{
    protected void Application_AuthenticateRequest(object sender, EventArgs e) {}
    protected void Application_BeginRequest(object sender, EventArgs e) {}
    protected void Application_End(object sender, EventArgs e) {}
    protected void Application_EndRequest(object sender, EventArgs e) {}
    protected void Application_Error(object sender, EventArgs e) {}
    protected void Application_Init(object sender, EventArgs e) {}
    protected void Application_Start(object sender, EventArgs e) {}
    protected void Session_End(object sender, EventArgs e) {}
    protected void Session_Start(object sender, EventArgs e) {}
}

public class InvalidContext
{
    protected void {|#0:Application_AuthenticateRequest|}(object sender, EventArgs e) {}
    protected void {|#1:Application_BeginRequest|}(object sender, EventArgs e) {}
    protected void {|#2:Application_End|}(object sender, EventArgs e) {}
    protected void {|#3:Application_EndRequest|}(object sender, EventArgs e) {}
    protected void {|#4:Application_Error|}(object sender, EventArgs e) {}
    protected void {|#5:Application_Init|}(object sender, EventArgs e) {}
    protected void {|#6:Application_Start|}(object sender, EventArgs e) {}
    protected void {|#7:Session_End|}(object sender, EventArgs e) {}
    protected void {|#8:Session_Start|}(object sender, EventArgs e) {}
}", new[]
{
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("InvalidContext.Application_AuthenticateRequest(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("InvalidContext.Application_BeginRequest(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("InvalidContext.Application_End(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("InvalidContext.Application_EndRequest(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(4).WithArguments("InvalidContext.Application_Error(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(5).WithArguments("InvalidContext.Application_Init(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(6).WithArguments("InvalidContext.Application_Start(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(7).WithArguments("InvalidContext.Session_End(object, System.EventArgs)"),
    VerifyCS.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(8).WithArguments("InvalidContext.Session_Start(object, System.EventArgs)")
}, @"
using System;

namespace System.Web
{
    public class HttpApplication {}
}

public class ValidContext : System.Web.HttpApplication
{
    protected void Application_AuthenticateRequest(object sender, EventArgs e) {}
    protected void Application_BeginRequest(object sender, EventArgs e) {}
    protected void Application_End(object sender, EventArgs e) {}
    protected void Application_EndRequest(object sender, EventArgs e) {}
    protected void Application_Error(object sender, EventArgs e) {}
    protected void Application_Init(object sender, EventArgs e) {}
    protected void Application_Start(object sender, EventArgs e) {}
    protected void Session_End(object sender, EventArgs e) {}
    protected void Session_Start(object sender, EventArgs e) {}
}

public class InvalidContext
{
    protected void ApplicationAuthenticateRequest(object sender, EventArgs e) {}
    protected void ApplicationBeginRequest(object sender, EventArgs e) {}
    protected void ApplicationEnd(object sender, EventArgs e) {}
    protected void ApplicationEndRequest(object sender, EventArgs e) {}
    protected void ApplicationError(object sender, EventArgs e) {}
    protected void ApplicationInit(object sender, EventArgs e) {}
    protected void ApplicationStart(object sender, EventArgs e) {}
    protected void SessionEnd(object sender, EventArgs e) {}
    protected void SessionStart(object sender, EventArgs e) {}
}");
        }

        #endregion

        #region Visual Basic Tests
        [Fact]
        public async Task CA1707_ForAssembly_VisualBasic()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Public Class DoesNotMatter
End Class
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                        solution.WithProjectAssemblyName(projectId, "AssemblyNameHasUnderScore_")
                },
                ExpectedDiagnostics =
                {
                    GetCA1707BasicResultAt(line: 2, column: 1, symbolKind: SymbolKind.Assembly, identifierNames: "AssemblyNameHasUnderScore_")
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1707_ForAssembly_NoDiagnostics_VisualBasic()
        {
            await new VerifyVB.Test
            {
                TestCode = @"
Public Class DoesNotMatter
End Class
",
                SolutionTransforms =
                {
                    (solution, projectId) =>
                        solution.WithProjectAssemblyName(projectId, "AssemblyNameHasNoUnderScore")
                }
            }.RunAsync();
        }

        [Fact]
        public async Task CA1707_ForNamespace_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Namespace OuterNamespace
    Namespace {|#0:HasUnderScore_|}
        Public Class DoesNotMatter
        End Class
    End Namespace
End Namespace

Namespace HasNoUnderScore
    Public Class DoesNotMatter
    End Class
End Namespace",
            VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.NamespaceRule).WithLocation(0).WithArguments("OuterNamespace.HasUnderScore_"), @"
Namespace OuterNamespace
    Namespace HasUnderScore
        Public Class DoesNotMatter
        End Class
    End Namespace
End Namespace

Namespace HasNoUnderScore
    Public Class DoesNotMatter
    End Class
End Namespace");
        }

        [Fact]
        public async Task CA1707_ForTypes_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class OuterType
    Public Class {|#0:UnderScoreInName_|}
    End Class

    Private Class UnderScoreInNameButPrivate_
    End Class
End Class",
            VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.TypeRule).WithLocation(0).WithArguments("OuterType.UnderScoreInName_"), @"
Public Class OuterType
    Public Class UnderScoreInName
    End Class

    Private Class UnderScoreInNameButPrivate_
    End Class
End Class");
        }

        [Fact]
        public async Task CA1707_ForFields_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter
    Public Const {|#0:ConstField_|} As Integer = 5
    Public Shared ReadOnly {|#1:SharedReadOnlyField_|} As Integer = 5

    ' No diagnostics for the below
    Private InstanceField_ As String
    Private Shared StaticField_ As String
    Public _field As String
    Protected Another_field As String
End Class

Public Enum DoesNotMatterEnum
    {|#2:_EnumWithUnderscore|}
End Enum", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.ConstField_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.SharedReadOnlyField_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("DoesNotMatterEnum._EnumWithUnderscore")
}, @"
Public Class DoesNotMatter
    Public Const ConstField As Integer = 5
    Public Shared ReadOnly SharedReadOnlyField As Integer = 5

    ' No diagnostics for the below
    Private InstanceField_ As String
    Private Shared StaticField_ As String
    Public _field As String
    Protected Another_field As String
End Class

Public Enum DoesNotMatterEnum
    EnumWithUnderscore
End Enum");
        }

        [Fact]
        public async Task CA1707_ForMethods_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter
    Public Sub {|#0:PublicM1_|}()
    End Sub
    ' No diagnostic
    Private Sub PrivateM2_()
    End Sub
    ' No diagnostic
    Friend Sub InternalM3_()
    End Sub
    Protected Sub {|#1:ProtectedM4_|}()
    End Sub
End Class

Public Interface I1
    Sub {|#2:M_|}()
End Interface

Public Class ImplementI1
    Implements I1
    Public Sub M_() Implements I1.M_
    End Sub
    ' No diagnostic
    Public Overridable Sub {|#3:M2_|}()
    End Sub
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Overrides Sub M2_()
    End Sub
End Class", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.PublicM1_()"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedM4_()"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("I1.M_()"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("ImplementI1.M2_()")
}, @"
Public Class DoesNotMatter
    Public Sub PublicM1()
    End Sub
    ' No diagnostic
    Private Sub PrivateM2_()
    End Sub
    ' No diagnostic
    Friend Sub InternalM3_()
    End Sub
    Protected Sub ProtectedM4()
    End Sub
End Class

Public Interface I1
    Sub M()
End Interface

Public Class ImplementI1
    Implements I1
    Public Sub M() Implements I1.M
    End Sub
    ' No diagnostic
    Public Overridable Sub M2()
    End Sub
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Overrides Sub M2()
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1707_ForProperties_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter
    Public Property {|#0:PublicP1_|}() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    ' No diagnostic
    Private Property PrivateP2_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    ' No diagnostic
    Friend Property InternalP3_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    Protected Property {|#1:ProtectedP4_|}() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class

Public Interface I1
    Property {|#2:P_|}() As Integer
End Interface

Public Class ImplementI1
    Implements I1
    ' No diagnostic
    Public Property P_() As Integer Implements I1.P_
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    Public Overridable Property {|#3:P2_|}() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Overrides Property P2_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.PublicP1_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedP4_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("I1.P_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("ImplementI1.P2_")
}, @"
Public Class DoesNotMatter
    Public Property PublicP1() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    ' No diagnostic
    Private Property PrivateP2_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    ' No diagnostic
    Friend Property InternalP3_() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    Protected Property ProtectedP4() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class

Public Interface I1
    Property P() As Integer
End Interface

Public Class ImplementI1
    Implements I1
    ' No diagnostic
    Public Property P() As Integer Implements I1.P
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
    Public Overridable Property P2() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class

Public Class Derives
    Inherits ImplementI1
    ' No diagnostic
    Public Overrides Property P2() As Integer
        Get
            Return 0
        End Get
        Set
        End Set
    End Property
End Class");
        }

        [Fact]
        public async Task CA1707_ForEvents_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter
    Public Event {|#0:PublicE1_|} As System.EventHandler
    Private Event PrivateE2_ As System.EventHandler
    ' No diagnostic
    Friend Event InternalE3_ As System.EventHandler
    ' No diagnostic
    Protected Event {|#1:ProtectedE4_|} As System.EventHandler
End Class

Public Interface I1
    Event {|#2:E_|} As System.EventHandler
End Interface

Public Class ImplementI1
    Implements I1
    ' No diagnostic
    Public Event E_ As System.EventHandler Implements I1.E_
    Public Event {|#3:E2_|} As System.EventHandler
End Class

Public Class Derives
    Inherits ImplementI1

    'Public Shadows Event E2_ As System.EventHandler ' Currently not renamed due to https://github.com/dotnet/roslyn/issues/46663
End Class", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("DoesNotMatter.PublicE1_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedE4_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("I1.E_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("ImplementI1.E2_"),
}, @"
Public Class DoesNotMatter
    Public Event PublicE1 As System.EventHandler
    Private Event PrivateE2_ As System.EventHandler
    ' No diagnostic
    Friend Event InternalE3_ As System.EventHandler
    ' No diagnostic
    Protected Event ProtectedE4 As System.EventHandler
End Class

Public Interface I1
    Event E As System.EventHandler
End Interface

Public Class ImplementI1
    Implements I1
    ' No diagnostic
    Public Event E As System.EventHandler Implements I1.E
    Public Event E2 As System.EventHandler
End Class

Public Class Derives
    Inherits ImplementI1

    'Public Shadows Event E2_ As System.EventHandler ' Currently not renamed due to https://github.com/dotnet/roslyn/issues/46663
End Class");
        }

        [Fact]
        public async Task CA1707_ForDelegates_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Delegate Sub Dele({|#0:intPublic_|} As Integer, {|#1:stringPublic_|} As String)
' No diagnostics
Friend Delegate Sub Dele2(intInternal_ As Integer, stringInternal_ As String)
Public Delegate Function Del(Of T)({|#2:t_|} As Integer) As T
", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule).WithLocation(0).WithArguments("Dele", "intPublic_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule).WithLocation(1).WithArguments("Dele", "stringPublic_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule).WithLocation(2).WithArguments("Del(Of T)", "t_")
}, @"
Public Delegate Sub Dele(intPublic As Integer, stringPublic As String)
' No diagnostics
Friend Delegate Sub Dele2(intInternal_ As Integer, stringInternal_ As String)
Public Delegate Function Del(Of T)(t As Integer) As T
");
        }

        [Fact]
        public async Task CA1707_ForMemberparameters_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter
    Public Sub PublicM1({|#0:int_|} As Integer)
    End Sub
    Private Sub PrivateM2(int_ As Integer)
    End Sub
    ' No diagnostic
    Friend Sub InternalM3(int_ As Integer)
    End Sub
    ' No diagnostic
    Protected Sub ProtectedM4({|#1:int_|} As Integer)
    End Sub
End Class

Public Interface I
    Sub M({|#2:int_|} As Integer)
End Interface

Public Class implementI
    Implements I
    Private Sub I_M(int_ As Integer) Implements I.M
    End Sub
End Class

Public MustInherit Class Base
    Public Overridable Sub M1({|#3:int_|} As Integer)
    End Sub

    Public MustOverride Sub M2({|#4:int_|} As Integer)
End Class

Public Class Der
    Inherits Base
    Public Overrides Sub M2(int_ As Integer)
        Throw New System.NotImplementedException()
    End Sub

    Public Overrides Sub M1(int_ As Integer)
        MyBase.M1(int_)
    End Sub
End Class", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(0).WithArguments("DoesNotMatter.PublicM1(Integer)", "int_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(1).WithArguments("DoesNotMatter.ProtectedM4(Integer)", "int_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(2).WithArguments("I.M(Integer)", "int_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(3).WithArguments("Base.M1(Integer)", "int_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule).WithLocation(4).WithArguments("Base.M2(Integer)", "int_")
}, @"
Public Class DoesNotMatter
    Public Sub PublicM1(int As Integer)
    End Sub
    Private Sub PrivateM2(int_ As Integer)
    End Sub
    ' No diagnostic
    Friend Sub InternalM3(int_ As Integer)
    End Sub
    ' No diagnostic
    Protected Sub ProtectedM4(int As Integer)
    End Sub
End Class

Public Interface I
    Sub M(int As Integer)
End Interface

Public Class implementI
    Implements I
    Private Sub I_M(int_ As Integer) Implements I.M
    End Sub
End Class

Public MustInherit Class Base
    Public Overridable Sub M1(int As Integer)
    End Sub

    Public MustOverride Sub M2(int As Integer)
End Class

Public Class Der
    Inherits Base
    Public Overrides Sub M2(int_ As Integer)
        Throw New System.NotImplementedException()
    End Sub

    Public Overrides Sub M1(int_ As Integer)
        MyBase.M1(int_)
    End Sub
End Class");
        }

        [Fact]
        public async Task CA1707_ForTypeTypeParameters_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter(Of {|#0:T_|})
End Class

Class NoDiag(Of U_)
End Class",
            VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.TypeTypeParameterRule).WithLocation(0).WithArguments("DoesNotMatter(Of T_)", "T_"), @"
Public Class DoesNotMatter(Of T)
End Class

Class NoDiag(Of U_)
End Class");
        }

        [Fact]
        public async Task CA1707_ForMemberTypeParameters_VisualBasic()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Public Class DoesNotMatter22
    Public Sub PublicM1(Of {|#0:T1_|})()
    End Sub
    Private Sub PrivateM2(Of U_)()
    End Sub
    Friend Sub InternalM3(Of W_)()
    End Sub
    Protected Sub ProtectedM4(Of {|#1:D_|})()
    End Sub
End Class

Public Interface I
    Sub M(Of {|#2:T_|})()
End Interface

Public Class implementI
    Implements I
    Public Sub M(Of U_)() Implements I.M
        Throw New System.NotImplementedException()
    End Sub
End Class

Public MustInherit Class Base
    Public Overridable Sub M1(Of {|#3:T_|})()
    End Sub

    Public MustOverride Sub M2(Of {|#4:U_|})()
End Class

Public Class Der
    Inherits Base
    Public Overrides Sub M2(Of U_)()
        Throw New System.NotImplementedException()
    End Sub

    Public Overrides Sub M1(Of T_)()
        MyBase.M1(Of T_)()
    End Sub
End Class", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(0).WithArguments("DoesNotMatter22.PublicM1(Of T1_)()", "T1_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(1).WithArguments("DoesNotMatter22.ProtectedM4(Of D_)()", "D_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(2).WithArguments("I.M(Of T_)()", "T_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(3).WithArguments("Base.M1(Of T_)()", "T_"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule).WithLocation(4).WithArguments("Base.M2(Of U_)()", "U_")
}, @"
Public Class DoesNotMatter22
    Public Sub PublicM1(Of T1)()
    End Sub
    Private Sub PrivateM2(Of U_)()
    End Sub
    Friend Sub InternalM3(Of W_)()
    End Sub
    Protected Sub ProtectedM4(Of D)()
    End Sub
End Class

Public Interface I
    Sub M(Of T)()
End Interface

Public Class implementI
    Implements I
    Public Sub M(Of U_)() Implements I.M
        Throw New System.NotImplementedException()
    End Sub
End Class

Public MustInherit Class Base
    Public Overridable Sub M1(Of T)()
    End Sub

    Public MustOverride Sub M2(Of U)()
End Class

Public Class Der
    Inherits Base
    Public Overrides Sub M2(Of U_)()
        Throw New System.NotImplementedException()
    End Sub

    Public Overrides Sub M1(Of T_)()
        MyBase.M1(Of T_)()
    End Sub
End Class");
        }

        [Fact, WorkItem(947, "https://github.com/dotnet/roslyn-analyzers/issues/947")]
        public async Task CA1707_ForOperators_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Structure S
	Public Shared Operator =(left As S, right As S) As Boolean
		Return left.Equals(right)
	End Operator

	Public Shared Operator <>(left As S, right As S) As Boolean
		Return Not (left = right)
	End Operator
End Structure
");
        }

        [Fact, WorkItem(1319, "https://github.com/dotnet/roslyn-analyzers/issues/1319")]
        public async Task CA1707_CustomOperator_VisualBasic()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class Span
    Public Shared Narrowing Operator CType(ByVal text As String) As Span
        Return New Span(text)
    End Operator

    Public Shared Widening Operator CType(ByVal span As Span) As String
        Return span.GetText()
    End Operator

    Private _text As String
    Public Sub New(ByVal text)
        _text = text
    End Sub

    Public Function GetText() As String
        Return _text
    End Function
End Class
");
        }

        [Fact, WorkItem(3121, "https://github.com/dotnet/roslyn-analyzers/issues/3121")]
        public async Task CA1707_VisualBasic_GlobalAsaxSpecialMethods()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System

Namespace System.Web
    Public Class HttpApplication
    End Class
End Namespace

Public Class ValidContext
    Inherits System.Web.HttpApplication

    Protected Sub Application_AuthenticateRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_BeginRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_End(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_EndRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Error(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Init(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Start(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Session_End(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Session_Start(ByVal sender As Object, ByVal e As EventArgs)
    End Sub
End Class

Public Class InvalidContext
    Protected Sub {|#0:Application_AuthenticateRequest|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#1:Application_BeginRequest|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#2:Application_End|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#3:Application_EndRequest|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#4:Application_Error|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#5:Application_Init|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#6:Application_Start|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#7:Session_End|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub {|#8:Session_Start|}(ByVal sender As Object, ByVal e As EventArgs)
    End Sub
End Class", new[]
{
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(0).WithArguments("InvalidContext.Application_AuthenticateRequest(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(1).WithArguments("InvalidContext.Application_BeginRequest(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(2).WithArguments("InvalidContext.Application_End(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(3).WithArguments("InvalidContext.Application_EndRequest(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(4).WithArguments("InvalidContext.Application_Error(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(5).WithArguments("InvalidContext.Application_Init(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(6).WithArguments("InvalidContext.Application_Start(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(7).WithArguments("InvalidContext.Session_End(Object, System.EventArgs)"),
    VerifyVB.Diagnostic(IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule).WithLocation(8).WithArguments("InvalidContext.Session_Start(Object, System.EventArgs)")
}, @"
Imports System

Namespace System.Web
    Public Class HttpApplication
    End Class
End Namespace

Public Class ValidContext
    Inherits System.Web.HttpApplication

    Protected Sub Application_AuthenticateRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_BeginRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_End(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_EndRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Error(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Init(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Application_Start(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Session_End(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub Session_Start(ByVal sender As Object, ByVal e As EventArgs)
    End Sub
End Class

Public Class InvalidContext
    Protected Sub ApplicationAuthenticateRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub ApplicationBeginRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub ApplicationEnd(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub ApplicationEndRequest(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub ApplicationError(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub ApplicationInit(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub ApplicationStart(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub SessionEnd(ByVal sender As Object, ByVal e As EventArgs)
    End Sub

    Protected Sub SessionStart(ByVal sender As Object, ByVal e As EventArgs)
    End Sub
End Class");
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCA1707CSharpResultAt(int line, int column, SymbolKind symbolKind, params string[] identifierNames)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(GetApproriateRule(symbolKind))
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(identifierNames);

        private static DiagnosticResult GetCA1707BasicResultAt(int line, int column, SymbolKind symbolKind, params string[] identifierNames)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(GetApproriateRule(symbolKind))
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(identifierNames);

        private static DiagnosticDescriptor GetApproriateRule(SymbolKind symbolKind)
        {
            return symbolKind switch
            {
                SymbolKind.Assembly => IdentifiersShouldNotContainUnderscoresAnalyzer.AssemblyRule,
                SymbolKind.Namespace => IdentifiersShouldNotContainUnderscoresAnalyzer.NamespaceRule,
                SymbolKind.NamedType => IdentifiersShouldNotContainUnderscoresAnalyzer.TypeRule,
                SymbolKind.Member => IdentifiersShouldNotContainUnderscoresAnalyzer.MemberRule,
                SymbolKind.DelegateParameter => IdentifiersShouldNotContainUnderscoresAnalyzer.DelegateParameterRule,
                SymbolKind.MemberParameter => IdentifiersShouldNotContainUnderscoresAnalyzer.MemberParameterRule,
                SymbolKind.TypeTypeParameter => IdentifiersShouldNotContainUnderscoresAnalyzer.TypeTypeParameterRule,
                SymbolKind.MethodTypeParameter => IdentifiersShouldNotContainUnderscoresAnalyzer.MethodTypeParameterRule,
                _ => throw new NotSupportedException("Unknown Symbol Kind"),
            };
        }

        private enum SymbolKind
        {
            Assembly,
            Namespace,
            NamedType,
            Member,
            DelegateParameter,
            MemberParameter,
            TypeTypeParameter,
            MethodTypeParameter
        }
        #endregion
    }
}
