// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.CancellationTokenParametersMustComeLastAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.CancellationTokenParametersMustComeLastAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    [TestClass]
    public class CancellationTokenParametersMustComeLast
    {
        [TestMethod]
        public async Task NoDiagnosticInEmptyFileAsync()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task DiagnosticForMethodAsync()
        {
            var source = @"
using System.Threading;
class T
{
    void M(CancellationToken t, int i)
    {
    }
}";
#pragma warning disable RS0030 // Do not use banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("T.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not use banned APIs
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task DiagnosticWhenFirstAndLastByOtherInBetweenAsync()
        {
            var source = @"
using System.Threading;
class T
{
    void M(CancellationToken t1, int i, CancellationToken t2)
    {
    }
}";
#pragma warning disable RS0030 // Do not use banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("T.M(System.Threading.CancellationToken, int, System.Threading.CancellationToken)");
#pragma warning restore RS0030 // Do not use banned APIs
            await VerifyCS.VerifyAnalyzerAsync(source, expected);
        }

        [TestMethod]
        public async Task NoDiagnosticWhenLastParamAsync()
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

        [TestMethod]
        public async Task NoDiagnosticWhenOnlyParamAsync()
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

        [TestMethod]
        public async Task NoDiagnosticWhenParamsComesAfterAsync()
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

        [TestMethod]
        public async Task NoDiagnosticWhenOutComesAfterAsync()
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

        [TestMethod]
        public async Task NoDiagnosticWhenRefComesAfterAsync()
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

        [TestMethod]
        public async Task NoDiagnosticWhenOptionalParameterComesAfterNonOptionalCancellationTokenAsync()
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

        [TestMethod]
        public async Task NoDiagnosticOnOverrideAsync()
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
#pragma warning disable RS0030 // Do not use banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 28).WithArguments("B.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not use banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task NoDiagnosticOnImplicitInterfaceImplementationAsync()
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
#pragma warning disable RS0030 // Do not use banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("I.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not use banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task NoDiagnosticOnExplicitInterfaceImplementationAsync()
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
#pragma warning disable RS0030 // Do not use banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 10).WithArguments("I.M(System.Threading.CancellationToken, int)");
#pragma warning restore RS0030 // Do not use banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod, WorkItem(1491, "https://github.com/dotnet/roslyn-analyzers/issues/1491")]
        public async Task NoDiagnosticOnCancellationTokenExtensionMethodAsync()
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

        [TestMethod, WorkItem(1816, "https://github.com/dotnet/roslyn-analyzers/issues/1816")]
        public async Task NoDiagnosticWhenMultipleAtEndOfParameterListAsync()
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

        [TestMethod]
        public async Task DiagnosticOnExtensionMethodWhenCancellationTokenIsNotFirstParameterAsync()
        {
            var test = @"
using System.Threading;
static class C1
{
    public static void M1(this object p1, CancellationToken p2, object p3)
    {
    }
}";

#pragma warning disable RS0030 // Do not use banned APIs
            var expected = VerifyCS.Diagnostic().WithLocation(5, 24).WithArguments("C1.M1(object, System.Threading.CancellationToken, object)");
#pragma warning restore RS0030 // Do not use banned APIs
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod, WorkItem(2281, "https://github.com/dotnet/roslyn-analyzers/issues/2281")]
        public async Task CA1068_DoNotReportOnIProgressLastAndCancellationTokenBeforeLastAsync()
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

        [TestMethod, WorkItem(2281, "https://github.com/dotnet/roslyn-analyzers/issues/2281")]
        public async Task CA1068_ReportOnIProgressLastAndCancellationTokenNotBeforeLastAsync()
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
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic().WithLocation(8, 17)
#pragma warning restore RS0030 // Do not use banned APIs
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
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic().WithLocation(7, 21)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments("Public Function SomeAsync(cancellationToken As System.Threading.CancellationToken, o As Object, progress As System.IProgress(Of Integer)) As System.Threading.Tasks.Task"));
        }

        [TestMethod, WorkItem(2281, "https://github.com/dotnet/roslyn-analyzers/issues/2281")]
        public async Task CA1068_OnlyExcludeOneIProgressAtTheEndAsync()
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
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic().WithLocation(8, 17)
#pragma warning restore RS0030 // Do not use banned APIs
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
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic().WithLocation(7, 21)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments("Public Function SomeAsync(cancellationToken As System.Threading.CancellationToken, progress1 As System.IProgress(Of Integer), progress2 As System.IProgress(Of Integer)) As System.Threading.Tasks.Task"));
        }

        [TestMethod, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithNonOptionalCancellationTokenAsync()
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

        [TestMethod, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithOptionalCancellationTokenAsync()
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

        [TestMethod, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithOptionalCancellationTokenAsLastParameterAsync()
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

        [TestMethod, WorkItem(4227, "https://github.com/dotnet/roslyn-analyzers/issues/4227")]
        public async Task CA1068_CallerAttributesWithOptionalCancellationTokenAsMiddleParameterAsync()
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

        [TestMethod, WorkItem(6557, "https://github.com/dotnet/roslyn-analyzers/issues/6557")]
        public async Task CA1068_CallerArgumentExpressionAttributeWithOptionalCancellationTokenAsLastParameterAsync()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(string input, [CallerArgumentExpression(nameof(input))] string argumentName = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}"
                   }
                }
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(6557, "https://github.com/dotnet/roslyn-analyzers/issues/6557")]
        public async Task CA1068_CallerArgumentExpressionAttributeWithOptionalCancellationTokenAsMiddleParameterAsync()
        {
            await new VerifyCS.Test
            {
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp9,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50,
                TestState =
                {
                    Sources =
                    {
                        @"
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public Task SomeAsync(string input, CancellationToken cancellationToken = default,
        [CallerArgumentExpression(nameof(input))] string argumentName = null)
    {
        throw new NotImplementedException();
    }
}"
                   }
                }
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(2851, "https://github.com/dotnet/roslyn-analyzers/issues/2851")]
        // Empty editorconfig
        [DataRow("public", "")]
        [DataRow("protected", "")]
        [DataRow("internal", "")]
        [DataRow("private", "")]
        // General analyzer option
        [DataRow("public", "dotnet_code_quality.api_surface = public")]
        [DataRow("public", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("public", "dotnet_code_quality.api_surface = all")]
        [DataRow("protected", "dotnet_code_quality.api_surface = public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = private, internal, public")]
        [DataRow("protected", "dotnet_code_quality.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.api_surface = internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = private, internal")]
        [DataRow("internal", "dotnet_code_quality.api_surface = all")]
        [DataRow("private", "dotnet_code_quality.api_surface = private")]
        [DataRow("private", "dotnet_code_quality.api_surface = private, public")]
        [DataRow("private", "dotnet_code_quality.api_surface = all")]
        // Specific analyzer option
        [DataRow("internal", "dotnet_code_quality.CA1068.api_surface = all")]
        [DataRow("internal", "dotnet_code_quality.Design.api_surface = all")]
        // General + Specific analyzer option
        [DataRow("internal", @"dotnet_code_quality.api_surface = private
                                  dotnet_code_quality.CA1068.api_surface = all")]
        // Case-insensitive analyzer option
        [DataRow("internal", "DOTNET_code_quality.CA1068.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("internal", @"dotnet_code_quality.api_surface = all
                                  dotnet_code_quality.CA1068.api_surface_2 = private")]
        public async Task CA1068_CSharp_ApiSurface_DiagnosticAsync(string accessibility, string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(2851, "https://github.com/dotnet/roslyn-analyzers/issues/2851")]
        // Empty editorconfig
        [DataRow("Public", "")]
        [DataRow("Protected", "")]
        [DataRow("Friend", "")]
        [DataRow("Private", "")]
        // General analyzer option
        [DataRow("Public", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Public", "dotnet_code_quality.api_surface = All")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = Private, Friend, Public")]
        [DataRow("Protected", "dotnet_code_quality.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = Private, Friend")]
        [DataRow("Friend", "dotnet_code_quality.api_surface = All")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private")]
        [DataRow("Private", "dotnet_code_quality.api_surface = Private, Public")]
        [DataRow("Private", "dotnet_code_quality.api_surface = All")]
        // Specific analyzer option
        [DataRow("Friend", "dotnet_code_quality.CA1068.api_surface = All")]
        [DataRow("Friend", "dotnet_code_quality.Design.api_surface = All")]
        // General + Specific analyzer option
        [DataRow("Friend", @"dotnet_code_quality.api_surface = Private
                                dotnet_code_quality.CA1068.api_surface = All")]
        // Case-insensitive analyzer option
        [DataRow("Friend", "DOTNET_code_quality.CA1068.API_SURFACE = ALL")]
        // Invalid analyzer option ignored
        [DataRow("Friend", @"dotnet_code_quality.api_surface = All
                                dotnet_code_quality.CA1068.api_surface_2 = Private")]
        public async Task CA1068_VisualBasic_ApiSurface_DiagnosticAsync(string accessibility, string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4467, "https://github.com/dotnet/roslyn-analyzers/issues/4467")]
        // No configuration - validate diagnostics in default configuration
        [DataRow(@"")]
        // Exclude all ctors
        [DataRow(@"dotnet_code_quality.excluded_symbol_names = .ctor")]
        // Exclude all members starting with C
        [DataRow(@"dotnet_code_quality.excluded_symbol_names = C*")]
        // Exclude classes C1 and C2
        [DataRow(@"dotnet_code_quality.excluded_symbol_names = T:C1|T:C2")]
        public async Task CA1068_ExcludedSymbolNames_DiagnosticAsync(string editorConfigText)
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
            }.RunAsync(CancellationToken.None);

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
            }.RunAsync(CancellationToken.None);
        }

        [TestMethod, WorkItem(4467, "https://github.com/dotnet/roslyn-analyzers/issues/4467")]
        // No configuration - validate diagnostics in default configuration
        [DataRow(@"")]
        // Exclude all ctors
        [DataRow(@"dotnet_code_quality.excluded_symbol_names = .ctor")]
        public async Task CA1068_ExcludedSymbolNames_Record_NoDiagnosticAsync(string editorConfigText)
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
            }.RunAsync(CancellationToken.None);
        }
    }
}
