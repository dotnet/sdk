// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.CancellationTokenParametersMustComeLastAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.CancellationTokenParametersMustComeLastAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class CancellationTokenParametersMustComeLast
    {
        [Fact]
        public async Task NoDiagnosticInEmptyFile()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task DiagnosticForMethod()
        {
            var source = @"
using System.Threading;
class T
{
    void M(CancellationToken t, int i)
    {
    }
}";
#pragma warning disable RS0030 // Do not used banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("T.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not used banned APIs
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task DiagnosticWhenFirstAndLastByOtherInBetween()
        {
            var source = @"
using System.Threading;
class T
{
    void M(CancellationToken t1, int i, CancellationToken t2)
    {
    }
}";
#pragma warning disable RS0030 // Do not used banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("T.M(System.Threading.CancellationToken, int, System.Threading.CancellationToken)");
#pragma warning restore RS0030 // Do not used banned APIs
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [Fact]
        public async Task NoDiagnosticWhenLastParam()
        {
            var test = @"
using System.Threading;
class T
{
    void M(int i, CancellationToken t)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticWhenOnlyParam()
        {
            var test = @"
using System.Threading;
class T
{
    void M(CancellationToken t)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticWhenParamsComesAfter()
        {
            var test = @"
using System.Threading;
class T
{
    void M(CancellationToken t, params object[] args)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticWhenOutComesAfter()
        {
            var test = @"
using System.Threading;
class T
{
    void M(CancellationToken t, out int i)
    {
        i = 2;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticWhenRefComesAfter()
        {
            var test = @"
using System.Threading;
class T
{
    void M(CancellationToken t, ref int x, ref int y)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticWhenOptionalParameterComesAfterNonOptionalCancellationToken()
        {
            var test = @"
using System.Threading;
class T
{
    void M(CancellationToken t, int x = 0)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnosticOnOverride()
        {
            var test = @"
using System.Threading;
class B
{
    protected virtual void M(CancellationToken t, int i) { }
}

class T : B
{
    protected override void M(CancellationToken t, int i) { }
}";

            // One diagnostic for the virtual, but none for the override.
#pragma warning disable RS0030 // Do not used banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 28).WithArguments("B.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not used banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task NoDiagnosticOnImplicitInterfaceImplementation()
        {
            var test = @"
using System.Threading;
interface I
{
    void M(CancellationToken t, int i);
}

class T : I
{
    public void M(CancellationToken t, int i) { }
}";

            // One diagnostic for the interface, but none for the implementation.
#pragma warning disable RS0030 // Do not used banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("I.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not used banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task NoDiagnosticOnExplicitInterfaceImplementation()
        {
            var test = @"
using System.Threading;
interface I
{
    void M(CancellationToken t, int i);
}

class T : I
{
    void I.M(CancellationToken t, int i) { }
}";

            // One diagnostic for the interface, but none for the implementation.
#pragma warning disable RS0030 // Do not used banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("I.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not used banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact, WorkItem(1491, "https://github.com/dotnet/roslyn-analyzers/issues/1491")]
        public async Task NoDiagnosticOnCancellationTokenExtensionMethod()
        {
            var test = @"
using System.Threading;
static class C1
{
    public static void M1(this CancellationToken p1, object p2)
    {
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact, WorkItem(1816, "https://github.com/dotnet/roslyn-analyzers/issues/1816")]
        public async Task NoDiagnosticWhenMultipleAtEndOfParameterList()
        {
            var test = @"
using System.Threading;
static class C1
{
    public static void M1(object p1, CancellationToken token1, CancellationToken token2) { }
    public static void M2(object p1, CancellationToken token1, CancellationToken token2, CancellationToken token3) { }
    public static void M3(CancellationToken token1, CancellationToken token2, CancellationToken token3) { }
    public static void M4(CancellationToken token1, CancellationToken token2 = default(CancellationToken)) { }
    public static void M5(CancellationToken token1 = default(CancellationToken), CancellationToken token2 = default(CancellationToken)) { }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task DiagnosticOnExtensionMethodWhenCancellationTokenIsNotFirstParameter()
        {
            var test = @"
using System.Threading;
static class C1
{
    public static void M1(this object p1, CancellationToken p2, object p3)
    {
    }
}";

#pragma warning disable RS0030 // Do not used banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 24).WithArguments("C1.M1(object, System.Threading.CancellationToken, object)");
#pragma warning restore RS0030 // Do not used banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact, WorkItem(2281, "https://github.com/dotnet/roslyn-analyzers/issues/2281")]
        public async Task CA1068_DoNotReportOnIProgressLastAndCancellationTokenBeforeLast()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(object o, CancellationToken cancellationToken, IProgress<int> progress)
    {
        throw new NotImplementedException();
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Public Class C
    Public Function SomeAsync(ByVal o As Object, ByVal cancellationToken As CancellationToken, ByVal progress As IProgress(Of Integer)) As Task
        Throw New NotImplementedException()
    End Function
End Class");
        }

        [Fact, WorkItem(2281, "https://github.com/dotnet/roslyn-analyzers/issues/2281")]
        public async Task CA1068_ReportOnIProgressLastAndCancellationTokenNotBeforeLast()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(CancellationToken cancellationToken, object o, IProgress<int> progress)
    {
        throw new NotImplementedException();
    }
}",
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic().WithLocation(8, 17)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments("C.SomeAsync(System.Threading.CancellationToken, object, System.IProgress<int>)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Public Class C
    Public Function SomeAsync(ByVal cancellationToken As CancellationToken, ByVal o As Object, ByVal progress As IProgress(Of Integer)) As Task
        Throw New NotImplementedException()
    End Function
End Class",
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic().WithLocation(7, 21)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments("Public Function SomeAsync(cancellationToken As System.Threading.CancellationToken, o As Object, progress As System.IProgress(Of Integer)) As System.Threading.Tasks.Task"));
        }

        [Fact, WorkItem(2281, "https://github.com/dotnet/roslyn-analyzers/issues/2281")]
        public async Task CA1068_OnlyExcludeOneIProgressAtTheEnd()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(CancellationToken cancellationToken, IProgress<int> progress1, IProgress<int> progress2)
    {
        throw new NotImplementedException();
    }
}",
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyCS.Diagnostic().WithLocation(8, 17)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments("C.SomeAsync(System.Threading.CancellationToken, System.IProgress<int>, System.IProgress<int>)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Threading
Imports System.Threading.Tasks

Public Class C
    Public Function SomeAsync(ByVal cancellationToken As CancellationToken, ByVal progress1 As IProgress(Of Integer), ByVal progress2 As IProgress(Of Integer)) As Task
        Throw New NotImplementedException()
    End Function
End Class",
#pragma warning disable RS0030 // Do not used banned APIs
            VerifyVB.Diagnostic().WithLocation(7, 21)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments("Public Function SomeAsync(cancellationToken As System.Threading.CancellationToken, progress1 As System.IProgress(Of Integer), progress2 As System.IProgress(Of Integer)) As System.Threading.Tasks.Task"));
        }

        [Fact, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithNonOptionalCancellationToken()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(CancellationToken cancellationToken,
        [CallerMemberName] string memberName = """",
        [CallerFilePath] string sourceFilePath = """",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        throw new NotImplementedException();
    }
}");
        }

        [Fact, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithOptionalCancellationToken()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(CancellationToken cancellationToken = default,
        [CallerMemberName] string memberName = """",
        [CallerFilePath] string sourceFilePath = """",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        throw new NotImplementedException();
    }
}");
        }

        [Fact, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithOptionalCancellationTokenAsLastParameter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync([CallerMemberName] string memberName = """",
        [CallerFilePath] string sourceFilePath = """",
        [CallerLineNumber] int sourceLineNumber = 0,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}");
        }

        [Fact, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithOptionalCancellationTokenAsMiddleParameter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync([CallerMemberName] string memberName = """",
        [CallerFilePath] string sourceFilePath = """",
        CancellationToken cancellationToken = default,
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        throw new NotImplementedException();
    }
}");
        }

        [Theory, WorkItem(2851, "https://github.com/dotnet/roslyn-analyzers/issues/2851")]
        // Empty editorconfig
        [InlineData("public", "")]
        [InlineData("protected", "")]
        [InlineData("internal", "")]
        [InlineData("private", "")]
        // General analyzer option
        [InlineData("public", "dotnet_code_quality.api_surface = public")]
        [InlineData("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("public", "dotnet_code_quality.api_surface = all")]
        [InlineData("protected", "dotnet_code_quality.api_surface = public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [InlineData("protected", "dotnet_code_quality.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.api_surface = internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = private, internal")]
        [InlineData("internal", "dotnet_code_quality.api_surface = all")]
        [InlineData("private", "dotnet_code_quality.api_surface = private")]
        [InlineData("private", "dotnet_code_quality.api_surface = private, public")]
        [InlineData("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [InlineData("internal", "dotnet_code_quality.CA1068.api_surface = all")]
        [InlineData("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [InlineData("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1068.api_surface = all")]
        // Case-insensitive analyzer option
        [InlineData("internal", "DOTNET_code_quality.CA1068.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1068.api_surface_2 = private")]
        public async Task CA1068_CSharp_ApiSurface_Diagnostic(string accessibility, string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
using System.Threading;

public class C
{{
    {accessibility} void [|M|](CancellationToken t, int i) {{}}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();
        }

        [Theory, WorkItem(2851, "https://github.com/dotnet/roslyn-analyzers/issues/2851")]
        // Empty editorconfig
        [InlineData("Public", "")]
        [InlineData("Protected", "")]
        [InlineData("Friend", "")]
        [InlineData("Private", "")]
        // General analyzer option
        [InlineData("Public", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Public", "dotnet_code_quality.api_surface = All")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [InlineData("Protected", "dotnet_code_quality.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [InlineData("Friend", "dotnet_code_quality.api_surface = All")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private")]
        [InlineData("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [InlineData("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [InlineData("Friend", "dotnet_code_quality.CA1068.api_surface = All")]
        [InlineData("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [InlineData("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1068.api_surface = All")]
        // Case-insensitive analyzer option
        [InlineData("Friend", "DOTNET_code_quality.CA1068.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [InlineData("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1068.api_surface_2 = Private")]
        public async Task CA1068_VisualBasic_ApiSurface_Diagnostic(string accessibility, string editorConfigText)
        {
            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System.Threading
Public Class C
    {accessibility} Sub [|M|](t As CancellationToken, i As Integer)
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();
        }

        [Theory, WorkItem(4467, "https://github.com/dotnet/roslyn-analyzers/issues/4467")]
        // No configuration - validate diagnostics in default configuration
        [InlineData(@"")]
        // Exclude all ctors
        [InlineData(@"dotnet_code_quality.excluded_symbol_names = .ctor")]
        // Exclude all members starting with C
        [InlineData(@"dotnet_code_quality.excluded_symbol_names = C*")]
        // Exclude classes C1 and C2
        [InlineData(@"dotnet_code_quality.excluded_symbol_names = T:C1|T:C2")]
        public async Task CA1068_ExcludedSymbolNames_Diagnostic(string editorConfigText)
        {
            var prefix = editorConfigText.Length == 0 ? "[|" : "";
            var suffix = editorConfigText.Length == 0 ? "|]" : "";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Threading;

public class C1
{
    public " + prefix + "C1" + suffix + @"(CancellationToken t, int i) {}

    public " + prefix + "C1" + suffix + @"(CancellationToken t, float f) {}
}

public class C2
{
    public " + prefix + "C2" + suffix + @"(CancellationToken t, int i) {}
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System.Threading

Public Class C1
    Public Sub " + prefix + "New" + suffix + @"(t As CancellationToken, i As Integer)
    End Sub

    Public Sub " + prefix + "New" + suffix + @"(t As CancellationToken, i As Single)
    End Sub
End Class

Public Class C2
    Public Sub " + prefix + "New" + suffix + @"(t As CancellationToken, i As Integer)
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();
        }

        [Theory, WorkItem(4467, "https://github.com/dotnet/roslyn-analyzers/issues/4467")]
        // No configuration - validate diagnostics in default configuration
        [InlineData(@"")]
        // Exclude all ctors
        [InlineData(@"dotnet_code_quality.excluded_symbol_names = .ctor")]
        public async Task CA1068_ExcludedSymbolNames_Record_NoDiagnostic(string editorConfigText)
        {
            var prefix = editorConfigText.Length == 0 ? "[|" : "";
            var suffix = editorConfigText.Length == 0 ? "|]" : "";

            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestState =
                {
                    Sources =
                    {
                        @"
using System.Threading;

public record " + prefix + "R" + suffix + @"(CancellationToken t, int i) {}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();
        }
    }
}
