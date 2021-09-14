// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotMatchKeywordsAnalyzer,
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpIdentifiersShouldNotMatchKeywordsFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.IdentifiersShouldNotMatchKeywordsAnalyzer,
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicIdentifiersShouldNotMatchKeywordsFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    /// <summary>
    /// Contains those unit tests for the IdentifiersShouldNotMatchKeywords analyzer that
    /// pertain to the MemberRule, which applies to the names of type members.
    /// </summary>
    public class IdentifiersShouldNotMatchKeywordsMemberRuleTests
    {
        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @internal() {}
}",
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub internal()
    End Sub
End Class
",
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassWithDifferentCasing()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @iNtErNaL() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassWithDifferentCasing()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub iNtErNaL()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpDiagnosticForCaseInsensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    // Matches VB AddHandler keyword:
    public virtual void aDdHaNdLeR() {}
}",
                GetCSharpResultAt(5, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.aDdHaNdLeR()", "AddHandler"));
        }

        [Fact]
        public async Task BasicDiagnosticForCaseInsensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    ' Matches VB AddHandler keyword:
    Public Overridable Sub [aDdHaNdLeR]()
    End Sub
End Class",
                GetBasicResultAt(4, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.aDdHaNdLeR()", "AddHandler"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedProtectedVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected virtual void @for() {}
}",
                GetCSharpResultAt(4, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedProtectedVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Overridable Sub [for]()
    End Sub
End Class",
                GetBasicResultAt(3, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedInternalVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    internal virtual void @for() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedInternalVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Friend Overridable Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedPublicNonVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void @for() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedPublicNonVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForNonKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void fort() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForNonKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub fort()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedVirtualMethodInInternalClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class C
{
    public virtual void @for() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedVirtualMethodInInternalClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class C
    Public Overridable Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedMethodInPublicInterface()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
    void @for();
}",
                GetCSharpResultAt(4, 10, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "I.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedMethodInPublicInterface()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Sub [for]()
End Interface",
                GetBasicResultAt(3, 9, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "I.for()", "for"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedMethodOfInternalInterface()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface I
{
    void @for();
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedMethodOfInternalInterface()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface I
    Sub [for]()
End Interface");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualPropertyOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private int _for;
    public virtual int @for
    {
        get
        {
            return _for;
        }
        set
        {
            _for = value;
        }
    }
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualPropertyOfPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Private _for As Integer
    Public Overridable Property [Sub] As Integer
        Get
            Return _for
        End Get
        Set(value As Integer)
            _for = value
        End Set
    End Property
End Class",
                GetBasicResultAt(4, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualAutoPropertyOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual int @for { get; set; }
}",
                GetCSharpResultAt(4, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualAutoPropertyOfPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Property [Sub] As Integer
End Class",
                GetBasicResultAt(3, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualReadOnlyPropertyOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private int _for;
    public virtual int @for
    {
        get
        {
            return _for;
        }
    }
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualReadOnlyPropertyOfPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Private _for As Integer
    Public Overridable ReadOnly Property [Sub] As Integer
        Get
            Return _for
        End Get
    End Property
End Class",
                GetBasicResultAt(4, 42, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualWriteOnlyPropertyOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private int _for;
    public virtual int @for
    {
        set
        {
            _for = value;
        }
    }
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualWriteOnlyPropertyOfPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Private _for As Integer
    Public Overridable WriteOnly Property [Sub] As Integer
        Set(value As Integer)
            _for = value
        End Set
    End Property
End Class",
                GetBasicResultAt(4, 43, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualExpressionBodyPropertyOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    private int _for;
    public virtual int @for => _for;
}",
                GetCSharpResultAt(5, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @internal() {}
}

public class D : C
{
    public override void @internal() {}
}",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public async Task BasicNoDiagnosticForOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub [internal]()
    End Sub
End Class

Public Class D
    Inherits C
    Public Overrides Sub [internal]()
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the override in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForSealedOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @internal() {}
}

public class D : C
{
    public sealed override void @internal() {}
}",
                // Diagnostic for the virtual in C, but none for the sealed override in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public async Task BasicNoDiagnosticForSealedOverrideOfKeywordNamedPublicVirtualMethodOfPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub [friend]()
    End Sub
End Class

Public Class D
    Inherits C
    Public NotOverridable Overrides Sub [friend]()
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the sealed override in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.friend()", "friend"));
        }

        [Fact]
        public async Task CSharpDiagnosticForEachOverloadOfCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @internal() {}
    public virtual void @internal(int n) {}
}",
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"),
                GetCSharpResultAt(5, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal(int)", "internal"));
        }

        [Fact]
        public async Task BasicDiagnosticForEachOverloadOfCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub internal()
    End Sub
    Public Overridable Sub internal(n As Integer)
    End Sub
End Class",
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"),
                GetBasicResultAt(5, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal(Integer)", "internal"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedNewMethodInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @for() {}
}

public class D : C
{
    public new void @for() {}
}",
                // Diagnostic for the virtual in C, but none for the new method in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedNewMethodInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub [for]()
    End Sub
End Class

Public Class D
    Inherits C

    Public Shadows Sub [for]()
    End Sub
End Class",
                // Diagnostic for the virtual in C, but none for the new method in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task CSharpDiagnosticForVirtualNewMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @for() {}
}

public class D : C
{
    public virtual new void @for() {}
}",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"),
                GetCSharpResultAt(9, 29, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "D.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForVirtualNewMethod()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub [for]()
    End Sub
End Class

Public Class D
    Inherits C

    Public Overridable Shadows Sub [for]()
    End Sub
End Class",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"),
                GetBasicResultAt(10, 36, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "D.for()", "for"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedProtectedVirtualMethodInProtectedTypeNestedInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected class D
    {
        protected virtual void @protected() {}
    }
}",
                GetCSharpResultAt(6, 32, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.D.protected()", "protected"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedProtectedVirtualMethodInProtectedTypeNestedInPublicClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Class D
        Protected Overridable Sub [Protected]()
        End Sub
    End Class
End Class",
                GetBasicResultAt(4, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.D.Protected()", "Protected"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedPublicVirtualEventInPublicClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public delegate void Callback(object sender, System.EventArgs e);
    public virtual event Callback @float;
}",
                // Diagnostics for both the virtual in C, and the virtual new method in D.
                GetCSharpResultAt(5, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.float", "float"));
        }

        // These tests are just to verify that the formatting of the displayed member name
        // is consistent with FxCop, for the case where the class is in a namespace.
        [Fact]
        public async Task CSharpDiagnosticForVirtualPublicMethodInPublicClassInNamespace()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace N
{
    public class C
    {
        public virtual void @for() {}
    }
}",
                // Don't include the namespace name.
                GetCSharpResultAt(6, 29, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForVirtualPublicMethodInPublicClassInNamespace()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Namespace N
    Public Class C
        Public Overridable Sub [for]()
        End Sub
    End Class
End Namespace",
                // Don't include the namespace name.
                GetBasicResultAt(4, 32, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        // These tests are just to verify that the formatting of the displayed member name
        // is consistent with FxCop, for the case where the class is generic.
        [Fact]
        public async Task CSharpDiagnosticForVirtualPublicMethodInPublicGenericClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C<T> where T : class
{
    public virtual void @for() {}
}",
                // Include the type parameter name but not the constraint.
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C<T>.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForVirtualPublicMethodInPublicGenericClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C(Of T As Class)
    Public Overridable Sub [for]()
    End Sub
End Class",
                // Include the type parameter name but not the constraint.
                GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C(Of T).for()", "for"));
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property")]
        public async Task UserOptionDoesNotIncludeMethod_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public virtual void @internal() {}
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class C
    Public Overridable Sub internal()
    End Sub
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Method")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Method")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Method")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Method")]
        public async Task UserOptionIncludesMethod_Diagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public virtual void @internal() {}
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"), },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class C
    Public Overridable Sub internal()
    End Sub
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetBasicResultAt(3, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"), },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Method")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Method")]
        public async Task UserOptionDoesNotIncludeProperty_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public virtual int @for { get; set; }
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class C
    Public Overridable Property [Sub] As Integer
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Property")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property")]
        public async Task UserOptionIncludesProperty_Diagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public virtual int @for { get; set; }
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetCSharpResultAt(4, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"), },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Public Class C
    Public Overridable Property [Sub] As Integer
End Class",
            },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetBasicResultAt(3, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"), },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Property")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Property")]
        public async Task UserOptionDoesNotIncludeEvent_NoDiagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public delegate void Callback(object sender, System.EventArgs e);
    public virtual event Callback @float;
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                },
            }.RunAsync();
        }

        [Theory]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = Event")]
        [InlineData("dotnet_code_quality.analyzed_symbol_kinds = NamedType, Event")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = Event")]
        [InlineData("dotnet_code_quality.CA1716.analyzed_symbol_kinds = NamedType, Event")]
        public async Task UserOptionIncludesEvent_Diagnostic(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
public class C
{
    public delegate void Callback(object sender, System.EventArgs e);
    public virtual event Callback @float;
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                    ExpectedDiagnostics = { GetCSharpResultAt(5, 35, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.float", "float"), },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, DiagnosticDescriptor rule, params string[] arguments)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(rule)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(arguments);
    }
}