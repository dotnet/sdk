// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Test.Utilities.MinimalImplementations;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.Maintainability.UnitTests
{
    public class DoNotIgnoreMethodResultsTests : DiagnosticAnalyzerTestBase
    {
        #region Unit tests for no analyzer diagnostic

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public void UsedInvocationResult()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void ExpectedExceptionLastLine()
        {
            VerifyCSharp(new[] { @"
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class Test
{
    [ExpectedException]
    public void ThrowsException()
    {
        new Test();
    }
}", MSTestAttributes.CSharp });

            VerifyBasic(new[] { @"
Imports System
Imports System.Globalization
Imports Microsoft.VisualStudio.TestTools.UnitTesting

Class C
    <ExpectedException>
    Public Sub ThrowsException()
        Console.WriteLine(Me)
        Dim sample As String = ""Sample""
        sample.ToLower(CultureInfo.InvariantCulture)
    End Sub
End Class", MSTestAttributes.VisualBasic });
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "Throws", "Exception", true)]
        [InlineData("Xunit", "ThrowsAny", "Exception", true)]
        [InlineData("NUnit.Framework", "Throws", "Exception", false)]
        [InlineData("NUnit.Framework", "Catch", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrow", "", false)]
        [Theory]
        public void UnitTestingThrows(string @namespace, string method, string generic, bool useXunit)
        {
            VerifyCSharp(new[] { $@"
using System;
using {@namespace};

public class Test
{{
    public void ThrowsException()
    {{
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"<{generic}>")}(() => {{ new Test(); }});
    }}
}}", useXunit ? XunitApis.CSharp : NUnitApis.CSharp });

            VerifyBasic(new[] { $@"
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
End Class", useXunit ? XunitApis.VisualBasic : NUnitApis.VisualBasic });
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "ThrowsAsync", "Exception", true)]
        [InlineData("Xunit", "ThrowsAnyAsync", "Exception", true)]
        [InlineData("NUnit.Framework", "ThrowsAsync", "Exception", false)]
        [InlineData("NUnit.Framework", "CatchAsync", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrowAsync", "", false)]
        [Theory]
        public void UnitTestingThrowsAsync(string @namespace, string method, string generic, bool useXunit)
        {
            VerifyCSharp(new[] { $@"
using System;
using System.Threading.Tasks;
using {@namespace};

public class Test
{{
    public void ThrowsException()
    {{
        Assert.{method}{(generic.Length == 0 ? string.Empty : $"<{generic}>")}(async () => {{ new Test(); }});
    }}
}}", useXunit ? XunitApis.CSharp : NUnitApis.CSharp });

            VerifyBasic(new[] { $@"
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
End Class", useXunit ? XunitApis.VisualBasic : NUnitApis.VisualBasic });
        }

        #endregion

        #region Unit tests for analyzer diagnostic(s)

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public void UnusedStringCreation()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void UnusedObjectCreation()
        {
            VerifyCSharp(@"
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
            VerifyBasic(@"
Imports System
Imports System.Globalization

Class C
    Public Sub DoesNotAssignObjectToVariable()
        New C() ' error BC30035: Syntax error
    End Sub
End Class
", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        [WorkItem(462, "https://github.com/dotnet/roslyn-analyzers/issues/462")]
        public void UnusedTryParseResult()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void UnusedPInvokeResult()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void UnusedComImportPreserveSig()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void UnusedPureMethodTriggersError()
        {
            VerifyCSharp(@"
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

            VerifyBasic(@"
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
        public void UnitTestingThrows_NotLastLineStillDiagnostic(string @namespace, string method, string generic, bool useXunit)
        {
            VerifyCSharp(new[] { $@"
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
}}", useXunit ? XunitApis.CSharp : NUnitApis.CSharp },
                GetCSharpObjectCreationResultAt(10, 13, "ThrowsException", "Test"));

            VerifyBasic(new[] { $@"
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
End Class", useXunit ? XunitApis.VisualBasic : NUnitApis.VisualBasic },
                GetBasicStringCreationResultAt(10, 41, "ThrowsException", "ToLower"));
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [InlineData("Xunit", "ThrowsAsync", "Exception", true)]
        [InlineData("Xunit", "ThrowsAnyAsync", "Exception", true)]
        [InlineData("NUnit.Framework", "ThrowsAsync", "Exception", false)]
        [InlineData("NUnit.Framework", "CatchAsync", "", false)]
        [InlineData("NUnit.Framework", "DoesNotThrowAsync", "", false)]
        [Theory]
        public void UnitTestingThrowsAsync_NotLastLineStillDiagnostic(string @namespace, string method, string generic, bool useXunit)
        {
            VerifyCSharp(new[] { $@"
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
}}", useXunit ? XunitApis.CSharp : NUnitApis.CSharp },
                GetCSharpObjectCreationResultAt(10, 13, "ThrowsException", "Test"));

            VerifyBasic(new[] { $@"
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
End Class", useXunit ? XunitApis.VisualBasic : NUnitApis.VisualBasic },
                GetBasicStringCreationResultAt(10, 41, "ThrowsException", "ToLower"));
        }

        [WorkItem(1369, "https://github.com/dotnet/roslyn-analyzers/issues/1369")]
        [Fact]
        public void ExpectedException_NotLastLineDiagnostic()
        {
            VerifyCSharp(new[] { @"
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class Test
{
    [ExpectedException]
    public void ThrowsException()
    {
        new Test();
        return;
    }
}", MSTestAttributes.CSharp },
                GetCSharpObjectCreationResultAt(9, 9, "ThrowsException", "Test"));

            VerifyBasic(new[] { @"
Imports System
Imports System.Globalization
Imports Microsoft.VisualStudio.TestTools.UnitTesting

Class C
    <ExpectedException>
    Public Sub ThrowsException()
        Console.WriteLine(Me)
        Dim sample As String = ""Sample""
        sample.ToLower(CultureInfo.InvariantCulture)
        Return
    End Sub
End Class", MSTestAttributes.VisualBasic },
                GetBasicStringCreationResultAt(11, 9, "ThrowsException", "ToLower"));
        }

        #endregion

        #region Helpers

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotIgnoreMethodResultsAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotIgnoreMethodResultsAnalyzer();
        }

        private static DiagnosticResult GetCSharpStringCreationResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageStringCreation, containingMethodName, invokedMethodName);
            return GetCSharpResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetBasicStringCreationResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageStringCreation, containingMethodName, invokedMethodName);
            return GetBasicResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCSharpObjectCreationResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageObjectCreation, containingMethodName, invokedMethodName);
            return GetCSharpResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCSharpTryParseResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageTryParse, containingMethodName, invokedMethodName);
            return GetCSharpResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetBasicTryParseResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageTryParse, containingMethodName, invokedMethodName);
            return GetBasicResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCSharpHResultOrErrorCodeResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageHResultOrErrorCode, containingMethodName, invokedMethodName);
            return GetCSharpResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetBasicHResultOrErrorCodeResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessageHResultOrErrorCode, containingMethodName, invokedMethodName);
            return GetBasicResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetCSharpPureMethodResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessagePureMethod, containingMethodName, invokedMethodName);
            return GetCSharpResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetBasicPureMethodResultAt(int line, int column, string containingMethodName, string invokedMethodName)
        {
            string message = string.Format(MicrosoftCodeQualityAnalyzersResources.DoNotIgnoreMethodResultsMessagePureMethod, containingMethodName, invokedMethodName);
            return GetBasicResultAt(line, column, DoNotIgnoreMethodResultsAnalyzer.RuleId, message);
        }

        #endregion
    }
}