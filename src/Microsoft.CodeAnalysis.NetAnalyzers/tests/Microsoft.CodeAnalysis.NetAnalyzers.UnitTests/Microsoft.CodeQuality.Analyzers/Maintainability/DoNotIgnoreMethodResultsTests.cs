// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.DoNotIgnoreMethodResultsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.Maintainability.DoNotIgnoreMethodResultsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class DoNotIgnoreMethodResultsTests
    {
        #region Unit tests for no analyzer diagnostic

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public async Task UsedInvocationResult()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

public class C
{
    private static void M(string x, out int y)
    {
        // Object creation
        var c = new C();
        
        // String creation
        var xUpper = x.ToUpper();

        // Try parse
        if (!int.TryParse(x, out y))
        {
            return;
        }

        var result = NativeMethod();
    }

    [DllImport(""user32.dll"")]
    private static extern int NativeMethod();
}
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Private Shared Sub M(x As String, ByRef y As Integer)
        ' Object creation
        Dim c = New C()

        ' String creation
        Dim xUpper = x.ToUpper()

        ' Try parse
        If Not Integer.TryParse(x, y) Then
            Return
        End If

        Dim result = NativeMethod()
    End Sub

    <DllImport(""user32.dll"")> _
    Private Shared Function NativeMethod() As Integer
    End Function
End Class
");
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [Fact]
        public async Task ExpectedExceptionLastLine()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMSTest,
                TestState =
                {
                    Sources =
                    {
                        @"
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class Test
{
    [ExpectedException(typeof(System.Exception))]
    public void ThrowsException()
    {
        new Test();
    }
}",
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMSTest,
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Globalization
Imports Microsoft.VisualStudio.TestTools.UnitTesting

Class C
    <ExpectedException(GetType(Exception))>
    Public Sub ThrowsException()
        Console.WriteLine(Me)
        Dim sample As String = ""Sample""
        sample.ToLower(CultureInfo.InvariantCulture)
    End Sub
End Class",
                    }
                }
            }.RunAsync();
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "Throws", "Exception", true)]
        [InlineData("Xunit", "ThrowsAny", "Exception", true)]
        [InlineData("NUnit.Framework", "Throws", "Exception", false)]
        [InlineData("NUnit.Framework", "Catch", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrow", "", false)]
        [Theory]
        public async Task UnitTestingThrows(string @namespace, string method, string generic, bool useXunit)
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;
using {@namespace};

public class Test
{{
    public void ThrowsException()
    {{
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"<{generic}>")}(() => {{ new Test(); }});
    }}
}}",
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System
Imports System.Globalization
Imports {@namespace}

Class C
    Public Sub ThrowsException()
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"(Of {generic})")}(Sub()
                                        Dim sample As String = ""Sample""
                                        sample.ToLower(CultureInfo.InvariantCulture)
                                    End Sub)
    End Sub
End Class",
                    }
                }
            }.RunAsync();
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "ThrowsAsync", "Exception", true)]
        [InlineData("Xunit", "ThrowsAnyAsync", "Exception", true)]
        [InlineData("NUnit.Framework", "ThrowsAsync", "Exception", false)]
        [InlineData("NUnit.Framework", "CatchAsync", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrowAsync", "", false)]
        [Theory]
        public async Task UnitTestingThrowsAsync(string @namespace, string method, string generic, bool useXunit)
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;
using System.Threading.Tasks;
using {@namespace};

public class Test
{{
    public void ThrowsException()
    {{
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"<{generic}>")}(async () => {{ new Test(); }});
    }}
}}",
                    }
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System
Imports System.Globalization
Imports {@namespace}

Class C
    Public Sub ThrowsException()
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"(Of {generic})")}(Async Function()
                                        Dim sample As String = ""Sample""
                                        sample.ToLower(CultureInfo.InvariantCulture)
                                    End Function)
    End Sub
End Class",
                    }
                }
            }.RunAsync();
        }

        #endregion

        #region Unit tests for analyzer diagnostic(s)

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public async Task UnusedStringCreation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

class C
{
    public void DoesNotAssignStringToVariable()
    {
        Console.WriteLine(this);
        string sample = ""Sample"";
        sample.ToLower(CultureInfo.InvariantCulture);
        return;
    }
}
",
    GetCSharpStringCreationResultAt(11, 9, "DoesNotAssignStringToVariable", "ToLower"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Class C
    Public Sub DoesNotAssignStringToVariable()
        Console.WriteLine(Me)
        Dim sample As String = ""Sample""
        sample.ToLower(CultureInfo.InvariantCulture)
        Return
    End Sub
End Class
",
    GetBasicStringCreationResultAt(9, 9, "DoesNotAssignStringToVariable", "ToLower"));
        }

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public async Task UnusedObjectCreation()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Globalization;

class C
{
    public void DoesNotAssignObjectToVariable()
    {
        new C();
    }
}
",
    GetCSharpObjectCreationResultAt(9, 9, "DoesNotAssignObjectToVariable", "C"));

            // Following code produces syntax error for VB, so no object creation diagnostic.
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Globalization

Class C
    Public Sub DoesNotAssignObjectToVariable()
        {|BC30035:New|} C()
    End Sub
End Class
");
        }

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public async Task UnusedTryParseResult()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

public class C
{
    private static void M(string x, out int y)
    {
        // Try parse
        int.TryParse(x, out y);
    }
}
",
    GetCSharpTryParseResultAt(9, 9, "M", "TryParse"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Private Shared Sub M(x As String, ByRef y As Integer)
        ' Try parse
        Integer.TryParse(x, y)
    End Sub
End Class
",
    GetBasicTryParseResultAt(7, 9, "M", "TryParse"));
        }

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public async Task UnusedPInvokeResult()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

public class C
{
    private static void M(string x, out int y)
    {
        y = 1;
        NativeMethod();
    }

    [DllImport(""user32.dll"")]
    private static extern int NativeMethod();
}
",
    GetCSharpHResultOrErrorCodeResultAt(9, 9, "M", "NativeMethod"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Private Shared Sub M(x As String, ByRef y As Integer)
        NativeMethod()
    End Sub

    <DllImport(""user32.dll"")> _
    Private Shared Function NativeMethod() As Integer
    End Function
End Class
",
    GetBasicHResultOrErrorCodeResultAt(6, 9, "M", "NativeMethod"));
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn-analyzers/issues/746")]
        [WorkItem(746, "https://github.com/dotnet/roslyn-analyzers/issues/746")]
        public async Task UnusedComImportPreserveSig()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Runtime.InteropServices;

public class C
{
    private static void M(IComClass cc)
    {
        cc.NativeMethod();
    }
}

[ComImport]
[Guid(""060DDE7F-A9CD-4669-A443-B6E25AF44E7C"")]
public interface IComClass
{
    [PreserveSig]
    int NativeMethod();
}
",
    GetCSharpHResultOrErrorCodeResultAt(8, 9, "M", "NativeMethod"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Runtime.InteropServices

Public Class C
    Private Shared Sub M(cc As IComClass)
        cc.NativeMethod()
    End Sub
End Class

<ComImport> _
<Guid(""060DDE7F-A9CD-4669-A443-B6E25AF44E7C"")> _
Public Interface IComClass
    <PreserveSig> _
    Function NativeMethod() As Integer
End Interface
",
    GetBasicHResultOrErrorCodeResultAt(6, 9, "M", "NativeMethod"));
        }

        [Fact]
        [WorkItem(1164, "https://github.com/dotnet/roslyn-analyzers/issues/1164")]
        public async Task UnusedPureMethodTriggersError()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics.Contracts;

class C
{
    [Pure]
    public int Returns1() => 1;

    public void DoesNotUseResult()
    {
        Returns1();
    }
}",
    GetCSharpPureMethodResultAt(11, 9, "DoesNotUseResult", "Returns1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Diagnostics.Contracts

Module Module1
    <Pure>
    Function Returns1() As Integer
        Return 1
    End Function

    Sub DoesNotUseResult()
        Returns1()
    End Sub

End Module
",
    GetBasicPureMethodResultAt(11, 9, "DoesNotUseResult", "Returns1"));
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "Throws", "Exception", true)]
        [InlineData("Xunit", "ThrowsAny", "Exception", true)]
        [InlineData("NUnit.Framework", "Throws", "Exception", false)]
        [InlineData("NUnit.Framework", "Catch", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrow", "", false)]
        [Theory]
        public async Task UnitTestingThrows_NotLastLineStillDiagnostic(string @namespace, string method, string generic, bool useXunit)
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;
using {@namespace};

public class Test
{{
    public void ThrowsException()
    {{
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"<{generic}>")}(() => {{
            new Test();
            return;
        }});
    }}
}}",
                    }
                },
                ExpectedDiagnostics =
                {
                    GetCSharpObjectCreationResultAt(10, 13, "ThrowsException", "Test"),
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System
Imports System.Globalization
Imports {@namespace}

Class C
    Public Sub ThrowsException()
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"(Of {generic})")}(Sub()
                                        Dim sample As String = ""Sample""
                                        sample.ToLower(CultureInfo.InvariantCulture)
                                        Return
                                    End Sub)
    End Sub
End Class",
                    }
                },
                ExpectedDiagnostics =
                {
                    GetBasicStringCreationResultAt(10, 41, "ThrowsException", "ToLower"),
                }
            }.RunAsync();
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "ThrowsAsync", "Exception", true)]
        [InlineData("Xunit", "ThrowsAnyAsync", "Exception", true)]
        [InlineData("NUnit.Framework", "ThrowsAsync", "Exception", false)]
        [InlineData("NUnit.Framework", "CatchAsync", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrowAsync", "", false)]
        [Theory]
        public async Task UnitTestingThrowsAsync_NotLastLineStillDiagnostic(string @namespace, string method, string generic, bool useXunit)
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
using System;
using {@namespace};

public class Test
{{
    public void ThrowsException()
    {{
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"<{generic}>")}(async () => {{
            new Test();
            return;
        }});
    }}
}}",
                    }
                },
                ExpectedDiagnostics =
                {
                    GetCSharpObjectCreationResultAt(10, 13, "ThrowsException", "Test"),
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = useXunit ? AdditionalMetadataReferences.DefaultWithXUnit : AdditionalMetadataReferences.DefaultWithNUnit,
                TestState =
                {
                    Sources =
                    {
                        $@"
Imports System
Imports System.Globalization
Imports {@namespace}

Class C
    Public Sub ThrowsException()
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"(Of {generic})")}(Async Function()
                                        Dim sample As String = ""Sample""
                                        sample.ToLower(CultureInfo.InvariantCulture)
                                        Return
                                    End Function)
    End Sub
End Class",
                    }
                },
                ExpectedDiagnostics =
                {
                    GetBasicStringCreationResultAt(10, 41, "ThrowsException", "ToLower"),
                }
            }.RunAsync();
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [Fact]
        public async Task ExpectedException_NotLastLineDiagnostic()
        {
            await new VerifyCS.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMSTest,
                TestState =
                {
                    Sources =
                    {
                        @"
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class Test
{
    [ExpectedException(typeof(System.Exception))]
    public void ThrowsException()
    {
        new Test();
        return;
    }
}",
                    }
                },
                ExpectedDiagnostics =
                {
                    GetCSharpObjectCreationResultAt(9, 9, "ThrowsException", "Test"),
                }
            }.RunAsync();

            await new VerifyVB.Test
            {
                ReferenceAssemblies = AdditionalMetadataReferences.DefaultWithMSTest,
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System
Imports System.Globalization
Imports Microsoft.VisualStudio.TestTools.UnitTesting

Class C
    <ExpectedException(GetType(Exception))>
    Public Sub ThrowsException()
        Console.WriteLine(Me)
        Dim sample As String = ""Sample""
        sample.ToLower(CultureInfo.InvariantCulture)
        Return
    End Sub
End Class",
                    }
                },
                ExpectedDiagnostics =
                {
                    GetBasicStringCreationResultAt(11, 9, "ThrowsException", "ToLower"),
                }
            }.RunAsync();
        }

        [Fact, WorkItem(3104, "https://github.com/dotnet/roslyn-analyzers/issues/3104")]
        public async Task PureMethodVoid()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.Diagnostics.Contracts;

public class A
{
    public int Write(string s) => 42;
}

public class B
{
    public string GetSomething()
    {
        WriteToDmm(""a"");
        return ""something"";
    }

    [Pure]
    private void WriteToDmm(string s) => new A().Write(s);
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.Diagnostics.Contracts

Public Class A
    Public Function Write(ByVal s As String) As Integer
        Return 42
    End Function
End Class

Public Class B
    Public Function GetSomething() As String
        WriteToDmm(""a"")
        Return ""something""
    End Function

    <Pure>
    Private Sub WriteToDmm(ByVal s As String)
        Dim x = New A().Write(s)
    End Sub
End Class");
        }

        #endregion

        #region Helpers

        private static DiagnosticResult GetCSharpStringCreationResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyCS.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.StringCreationRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetBasicStringCreationResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyVB.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.StringCreationRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetCSharpObjectCreationResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyCS.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.ObjectCreationRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetCSharpTryParseResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyCS.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.TryParseRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetBasicTryParseResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyVB.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.TryParseRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetCSharpHResultOrErrorCodeResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyCS.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.HResultOrErrorCodeRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetBasicHResultOrErrorCodeResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyVB.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.HResultOrErrorCodeRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetCSharpPureMethodResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyCS.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.PureMethodRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        private static DiagnosticResult GetBasicPureMethodResultAt(int line, int column, string containingMethodName, string invokedMethodName)
            => VerifyVB.Diagnostic(DoNotIgnoreMethodResultsAnalyzer.PureMethodRule)
                .WithLocation(line, column)
                .WithArguments(containingMethodName, invokedMethodName);

        #endregion
    }
}