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
        public async Task CSharpDiagnosticForKeywordNamedPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @internal() {}
}",
                GetCSharpResultAt(4, 25, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.internal()", "internal"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedPublicVirtualMethodInPublicClassAsync()
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
        public async Task CSharpNoDiagnosticForCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassWithDifferentCasingAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void @iNtErNaL() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassWithDifferentCasingAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub iNtErNaL()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpDiagnosticForCaseInsensitiveKeywordNamedPublicVirtualMethodInPublicClassAsync()
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
        public async Task BasicDiagnosticForCaseInsensitiveKeywordNamedPublicVirtualMethodInPublicClassAsync()
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
        public async Task CSharpDiagnosticForKeywordNamedProtectedVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    protected virtual void @for() {}
}",
                GetCSharpResultAt(4, 28, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedProtectedVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Protected Overridable Sub [for]()
    End Sub
End Class",
                GetBasicResultAt(3, 31, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for()", "for"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedInternalVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    internal virtual void @for() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedInternalVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Friend Overridable Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedPublicNonVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void @for() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedPublicNonVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForNonKeywordNamedPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual void fort() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForNonKeywordNamedPublicVirtualMethodInPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Sub fort()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedVirtualMethodInInternalClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal class C
{
    public virtual void @for() {}
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedVirtualMethodInInternalClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Class C
    Public Overridable Sub [for]()
    End Sub
End Class");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeywordNamedMethodInPublicInterfaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public interface I
{
    void @for();
}",
                GetCSharpResultAt(4, 10, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "I.for()", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeywordNamedMethodInPublicInterfaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Interface I
    Sub [for]()
End Interface",
                GetBasicResultAt(3, 9, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "I.for()", "for"));
        }

        [Fact]
        public async Task CSharpNoDiagnosticForKeywordNamedMethodOfInternalInterfaceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
internal interface I
{
    void @for();
}");
        }

        [Fact]
        public async Task BasicNoDiagnosticForKeywordNamedMethodOfInternalInterfaceAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Friend Interface I
    Sub [for]()
End Interface");
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualPropertyOfPublicClassAsync()
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
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualPropertyOfPublicClassAsync()
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
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualAutoPropertyOfPublicClassAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public virtual int @for { get; set; }
}",
                GetCSharpResultAt(4, 24, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.for", "for"));
        }

        [Fact]
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualAutoPropertyOfPublicClassAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Overridable Property [Sub] As Integer
End Class",
                GetBasicResultAt(3, 33, IdentifiersShouldNotMatchKeywordsAnalyzer.MemberRule, "C.Sub", "Sub"));
        }

        [Fact]
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualReadOnlyPropertyOfPublicClassAsync()
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
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualReadOnlyPropertyOfPublicClassAsync()
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
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualWriteOnlyPropertyOfPublicClassAsync()
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
        public async Task BasicDiagnosticForKeyWordNamedPublicVirtualWriteOnlyPropertyOfPublicClassAsync()
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
        public async Task CSharpDiagnosticForKeyWordNamedPublicVirtualExpressionBodyPropertyOfPublicClassAsync()
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
        public async Task CSharpNoDiagnosticForOverrideOfKeywordNamedPublicVirtualMethodOfPublicClassAsync()
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
        public async Task BasicNoDiagnosticForOverrideOfKeywordNamedPublicVirtualMethodOfPublicClassAsync()
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
        public async Task CSharpNoDiagnosticForSealedOverrideOfKeywordNamedPublicVirtualMethodOfPublicClassAsync()
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
        public async Task BasicNoDiagnosticForSealedOverrideOfKeywordNamedPublicVirtualMethodOfPublicClassAsync()
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
        public async Task CSharpDiagnosticForEachOverloadOfCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassAsync()
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
        public async Task BasicDiagnosticForEachOverloadOfCaseSensitiveKeywordNamedPublicVirtualMethodInPublicClassAsync()
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
        public async Task CSharpNoDiagnosticForKeywordNamedNewMethodInPublicClassAsync()
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
        public async Task BasicNoDiagnosticForKeywordNamedNewMethodInPublicClassAsync()
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
        public async Task CSharpDiagnosticForVirtualNewMethodAsync()
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
        public async Task BasicDiagnosticForVirtualNewMethodAsync()
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
        public async Task CSharpDiagnosticForKeywordNamedProtectedVirtualMethodInProtectedTypeNestedInPublicClassAsync()
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
        public async Task BasicDiagnosticForKeywordNamedProtectedVirtualMethodInProtectedTypeNestedInPublicClassAsync()
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
        public async Task CSharpDiagnosticForKeywordNamedPublicVirtualEventInPublicClassAsync()
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
        public async Task CSharpDiagnosticForVirtualPublicMethodInPublicClassInNamespaceAsync()
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
        public async Task BasicDiagnosticForVirtualPublicMethodInPublicClassInNamespaceAsync()
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
        public async Task CSharpDiagnosticForVirtualPublicMethodInPublicGenericClassAsync()
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
        public async Task BasicDiagnosticForVirtualPublicMethodInPublicGenericClassAsync()
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
        public async Task UserOptionDoesNotIncludeMethod_NoDiagnosticAsync(string editorConfigText)
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
        public async Task UserOptionIncludesMethod_DiagnosticAsync(string editorConfigText)
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
        public async Task UserOptionDoesNotIncludeProperty_NoDiagnosticAsync(string editorConfigText)
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
        public async Task UserOptionIncludesProperty_DiagnosticAsync(string editorConfigText)
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
        public async Task UserOptionDoesNotIncludeEvent_NoDiagnosticAsync(string editorConfigText)
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
        public async Task UserOptionIncludesEvent_DiagnosticAsync(string editorConfigText)
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