// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidOutParameters,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.AvoidOutParameters,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class AvoidOutParametersTests
    {
        [Fact]
        public async Task SimpleCases_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void [|M1|](out C c)
    {
        c = null;
    }

    public void [|M2|](string s, out C c)
    {
        c = null;
    }

    public void [|M3|](string s1, out C c, string s2)
    {
        c = null;
    }

    public void [|M4|](out C c, out string s1)
    {
        c = null;
        s1 = null;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub [|M1|](<Out> ByRef c As C)
        c = Nothing
    End Sub

    Public Sub [|M2|](ByVal s As String, <Out> ByRef c As C)
        c = Nothing
    End Sub

    Public Sub [|M3|](ByVal s1 As String, <Out> ByRef c As C, ByVal s2 As String)
        c = Nothing
    End Sub

    Public Sub [|M4|](<Out> ByRef c As C, <Out> ByRef s1 As String)
        c = Nothing
        s1 = Nothing
    End Sub
End Class");
        }

        [Theory]
        [InlineData("public", "dotnet_code_quality.api_surface = private")]
        [InlineData("private", "dotnet_code_quality.api_surface = internal, public")]
        [InlineData("public", "dotnet_code_quality.CA1021.api_surface = private")]
        [InlineData("private", "dotnet_code_quality.CA1021.api_surface = internal, public")]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                dotnet_code_quality.CA1021.api_surface = private")]
        public async Task ApiSurface_NoDiagnosticAsync(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void M(out string s)
    {{
        s = null;
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System.Runtime.InteropServices

Public Class C
    {accessibility} Sub M(<Out> ByRef s As String)
        s = Nothing
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            }.RunAsync();
        }

        [Theory]
        [InlineData("private", "dotnet_code_quality.api_surface = private")]
        [InlineData("internal", "dotnet_code_quality.api_surface = internal")]
        [InlineData("public", "dotnet_code_quality.api_surface = public")]
        [InlineData("private", "dotnet_code_quality.CA1021.api_surface = private")]
        [InlineData("internal", "dotnet_code_quality.CA1021.api_surface = internal")]
        [InlineData("public", "dotnet_code_quality.CA1021.api_surface = public")]
        [InlineData("public", @"dotnet_code_quality.api_surface = private
                                dotnet_code_quality.CA1021.api_surface = public")]
        public async Task ApiSurface_DiagnosticAsync(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void [|M|](out string s)
    {{
        s = null;
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            }.RunAsync();

            if (accessibility.Equals("internal", System.StringComparison.Ordinal))
            {
                accessibility = "Friend";
            }

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System.Runtime.InteropServices

Public Class C
    {accessibility} Sub [|M|](<Out> ByRef s As String)
        s = Nothing
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task MultipleOut_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void [|M1|](out C c, out string s1)
    {
        c = null;
        s1 = null;
    }

    public void [|M2|](out C c, string s1, out string s2)
    {
        c = null;
        s2 = null;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub [|M1|](<Out> ByRef c As C, <Out> ByRef s1 As String)
        c = Nothing
        s1 = Nothing
    End Sub

    Public Sub [|M2|](<Out> ByRef c As C, ByVal s1 As String, <Out> ByRef s2 As String)
        c = Nothing
        s2 = Nothing
    End Sub
End Class");
        }

        [Fact]
        public async Task OutAndRef_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void [|M1|](out C c, ref string s1)
    {
        c = null;
    }

    public void [|M2|](out C c, string s1, out string s2, ref string s3)
    {
        c = null;
        s2 = null;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub [|M1|](<Out> ByRef c As C, ByRef s1 As String)
        c = Nothing
    End Sub

    Public Sub [|M2|](<Out> ByRef c As C, ByVal s1 As String, <Out> ByRef s2 As String, ByRef s3 As String)
        c = Nothing
        s2 = Nothing
    End Sub
End Class");
        }

        [Fact]
        public async Task Ref_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void M(ref string s1)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Public Class C
    Public Sub M(ByRef s1 As String)
    End Sub
End Class");
        }

        [Fact]
        public async Task TryPattern_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Collections.Generic;

public class C
{
    private Dictionary<string, C> dict = new Dictionary<string, C>();

    public static bool TryParse(string s, out C result)
    {
        result = null;
        return false;
    }

    private bool TryGetOrAdd(string key, C valueIfNotFound, out C result)
    {
        if (dict.ContainsKey(key))
        {
            result = dict[key];
            return true;
        }

        dict[key] = valueIfNotFound;
        result = valueIfNotFound;
        return false;
    }

    public static bool Try(out C c)
    {
        c = null;
        return true;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Public Class C
    Private dict = New Dictionary(Of String, C)

    Public Shared Function TryParse(ByVal s As String, <Out> ByRef result As C) As Boolean
        result = Nothing
        Return False
    End Function

    Private Function TryGetOrAdd(ByVal key As String, ByVal valueIfNotFound As C, <Out> ByRef result As C) As Boolean
        If dict.ContainsKey(key) Then
            result = dict(key)
            Return True
        End If
        dict(key) = valueIfNotFound
        result = valueIfNotFound
        Return False
    End Function

    Public Shared Function [Try](<Out> ByRef c As C) As Boolean
        c = Nothing
        Return True
    End Function
End Class");
        }

        [Fact]
        public async Task InvalidTryPattern_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class C
{
    public void [|TryM1|](out C c)
    {
        c = null;
    }

    public bool [|TryM2|](out C c, string s)
    {
        c = null;
        return false;
    }

    public static bool [|TRY_PARSE|](string s, out C c)
    {
        c = null;
        return true;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Public Sub [|TryM1|](<Out> ByRef c As C)
        c = Nothing
    End Sub

    Public Function [|TryM2|](<Out> ByRef c As C, ByVal s As String) As Boolean
        c = Nothing
        Return False
    End Function

    Public Shared Function [|TRY_PARSE|](ByVal s As String, <Out> ByRef c As C) As Boolean
        c = Nothing
        Return True
    End Function
End Class");
        }

        [Fact]
        public async Task Deconstruct_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Person
{
    public void Deconstruct(out string fname, out string lname)
    {
        fname = null;
        lname = null;
    }
}");
        }

        [Fact]
        public async Task DeconstructExtensionMethod_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Person
{
    public string Name { get; set; }
}

public static class Ext
{
    public static void Deconstruct(this Person p, out string name)
    {
        name = p.Name;
    }
}");
        }

        [Fact]
        public async Task InvalidDeconstruct_DiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
public class Person
{
    public void [|Deconstruct|](out string fname, string lname)
    {
        fname = null;
    }
}");
        }

        [Fact]
        [WorkItem(5854, "https://github.com/dotnet/roslyn-analyzers/issues/5854")]
        public async Task MethodIsOverrideOrInterfaceImplementation_NoDiagnosticAsync()
        {
            var csSource = @"
public interface IInterface
{
    void [|InterfaceMethod|](out string s);
}

public abstract class Base
{
    public abstract void [|AbstractMethod|](out string s);
    public virtual void [|VirtualMethod|](out string s) => s = null;
}

public class Derived : Base
{
    public override void AbstractMethod(out string s) => s = null; // No diagnostic here. This is not actionable.

    public override void VirtualMethod(out string s) => s = null; // No diagnostic here. This is not actionable.
}

public class InterfaceImplicitImpl : IInterface
{
    public void InterfaceMethod(out string s) // No diagnostic here. This is not actionable.
    {
        throw new System.NotImplementedException();
    }
}

public class InterfaceExplicitImpl : IInterface
{
    void IInterface.InterfaceMethod(out string s) // No diagnostic here. This is not actionable.
    {
        throw new System.NotImplementedException();
    }
}

public class InterfaceBothImplicitAndExplicitImpl : IInterface
{
    // Possibly false positive.
    public void [|InterfaceMethod|](out string s) // No diagnostic here. This is not actionable.
    {
        throw new System.NotImplementedException();
    }

    void IInterface.InterfaceMethod(out string s) // No diagnostic here. This is not actionable.
    {
        throw new System.NotImplementedException();
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { csSource },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1021.api_surface = all
") },
                }
            }.RunAsync();

            var vbSource = @"
Imports System.Runtime.InteropServices

Public Interface IInterface
    Sub [|InterfaceMethod|](<Out> ByRef s As String)
End Interface

Public MustInherit Class Base
    Public MustOverride Sub [|AbstractMethod|](<Out> ByRef s As String)

    Public Overridable Sub [|VirtualMethod|](<Out> ByRef s As String)
        s = Nothing
    End Sub

End Class

Public Class Derived
    Inherits Base

    Public Overrides Sub AbstractMethod(<Out> ByRef s As String)
        s = Nothing
    End Sub

    Public Overrides Sub VirtualMethod(<Out> ByRef s As String)
        s = Nothing
    End Sub
End Class

Public Class InterfaceImplicitImpl
    Implements IInterface

    ' Possibly a false positive.
    Public Sub [|InterfaceMethod|](<Out> ByRef s As String)
        Throw New System.NotImplementedException()
    End Sub

    Private Sub IInterface_InterfaceMethod(<Out> ByRef s As String) Implements IInterface.InterfaceMethod
        Throw New System.NotImplementedException()
    End Sub
End Class
";
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources = { vbSource },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1021.api_surface = all
") },
                }
            }.RunAsync();
        }

        [Fact]
        public async Task TestTryPatternInterface()
        {
            var source = @"
public interface ITry
{
    bool TrySomething(out string something);
}

public class Try : ITry
{
    bool ITry.TrySomething(out string something)
    {
        something = null;
        return false;
    }

    public bool TryAnything(out string something)
    {
        return TrySecretly(out something);
    }

    private bool TrySecretly(out string something)
    {
        something = null;
        return false;
    }
}
";
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources = { source },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA1021.api_surface = all
") },
                }
            }.RunAsync();
        }
    }
}
