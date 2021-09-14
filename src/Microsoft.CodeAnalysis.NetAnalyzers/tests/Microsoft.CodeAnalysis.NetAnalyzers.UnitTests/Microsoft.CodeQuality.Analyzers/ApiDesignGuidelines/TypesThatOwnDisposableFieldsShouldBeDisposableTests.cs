// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines.CSharpTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines.BasicTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzerTests
    {
        [Fact]
        public async Task CA1001CSharpTestWithNoDisposableType()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
");
        }

        [Fact]
        public async Task CA1001CSharpTestWithNoCreationOfDisposableObject()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

    public class NoDisposeClass
    {
        FileStream newFile;
    }
");
        }

        [Fact]
        public async Task CA1001CSharpTestWithFieldInitAndNoDisposeMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

    public class NoDisposeClass
    {
        FileStream newFile1, newFile2 = new FileStream(""data.txt"", FileMode.Append);
    }
",
            GetCA1001CSharpResultAt(4, 18, "NoDisposeClass", "newFile2"));
        }

        [Fact]
        public async Task CA1001CSharpTestWithCtorInitAndNoDisposeMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

    // This class violates the rule.
    public class NoDisposeClass
    {
        FileStream newFile;

        public NoDisposeClass()
        {
            newFile = new FileStream(""data.txt"", FileMode.Append);
        }
    }
",
            GetCA1001CSharpResultAt(5, 18, "NoDisposeClass", "newFile"));
        }

        [Fact]
        public async Task CA1001CSharpTestWithCreationOfDisposableObjectInOtherClass()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

    public class NoDisposeClass
    {
        public FileStream newFile;
    }                 

    public class CreationClass
    {
        public void Create()
        {
            var obj = new NoDisposeClass() { newFile = new FileStream(""data.txt"", FileMode.Append) };
        }
    }
");

            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

    public class NoDisposeClass
    {
        public FileStream newFile;

        public NoDisposeClass(FileStream fs)
        {
            newFile = fs;
        }
    }
");
        }

        [Fact]
        public async Task CA1001CSharpTestWithNoDisposeMethodInScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

    // This class violates the rule.
    public class [|NoDisposeClass|]
    {
        FileStream newFile;

        public NoDisposeClass()
        {
            newFile = new FileStream(""data.txt"", FileMode.Append);
        }
    }
");
        }

        [Fact]
        public async Task CA1001CSharpScopedTestWithNoDisposeMethodOutOfScope()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

// This class violates the rule.
public class [|NoDisposeClass|]
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new FileStream(""data.txt"", FileMode.Append);
    }
}

public class SomeClass
{
}
");
        }

        [Fact]
        public async Task CA1001CSharpTestWithADisposeMethod()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

// This class satisfies the rule.
public class HasDisposeMethod : IDisposable
{
    FileStream newFile;

    public HasDisposeMethod()
    {
        newFile = new FileStream(""data.txt"", FileMode.Append);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // dispose managed resources
            newFile.Close();
        }
        // free native resources
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
");
        }

        [Fact, WorkItem(1562, "https://github.com/dotnet/roslyn-analyzers/issues/1562")]
        public async Task CA1001CSharpTestWithIDisposableField()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;

namespace ClassLibrary1
{
    public class Class1
    {
        private readonly IDisposable _disp1 = new MemoryStream();
    }
}
",
            GetCA1001CSharpResultAt(7, 18, "Class1", "_disp1"));
        }

        [Fact, WorkItem(1562, "https://github.com/dotnet/roslyn-analyzers/issues/1562")]
        public async Task CA1001CSharpTestWithIAsyncDisposable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
namespace System
{
    public class ValueTask {}

    public interface IAsyncDisposable
    {
        ValueTask DisposeAsync();
    }

    public sealed class Stream : System.IDisposable, IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new ValueTask();
        public void Dispose() {}
    }
}

namespace ClassLibrary1
{
    using System;

    public class Class1 : IAsyncDisposable
    {
        private readonly Stream _disposableMember = new Stream();

        public ValueTask DisposeAsync() => _disposableMember.DisposeAsync();
    }
}
");
        }

        [Fact]
        public async Task CA1001BasicTestWithNoDisposableType()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Module Module1

    Sub Main()

    End Sub

End Module
");
        }

        [Fact]
        public async Task CA1001BasicTestWithNoCreationOfDisposableObject()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO

    Public Class NoDisposeClass
	    Dim newFile As FileStream
    End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithFieldInitAndNoDisposeMethod()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
           
   ' This class violates the rule. 
    Public Class NoDisposeClass
        Dim newFile As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
",
            GetCA1001BasicResultAt(5, 18, "NoDisposeClass", "newFile"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
      
   ' This class violates the rule. 
    Public Class NoDisposeClass
        Dim newFile1 As FileStream, newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
",
            GetCA1001BasicResultAt(5, 18, "NoDisposeClass", "newFile2"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO
    
   ' This class violates the rule. 
    Public Class NoDisposeClass
        Dim newFile1 As FileStream
        Dim newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
",
            GetCA1001BasicResultAt(5, 18, "NoDisposeClass", "newFile2"));
        }

        [Fact]
        public async Task CA1001BasicTestWithCtorInitAndNoDisposeMethod()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
   Imports System
   Imports System.IO

   ' This class violates the rule. 
   Public Class NoDisposeMethod

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

   End Class
",
            GetCA1001BasicResultAt(6, 17, "NoDisposeMethod", "newFile"));
        }

        [Fact]
        public async Task CA1001BasicTestWithCreationOfDisposableObjectInOtherClass()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO

    Public Class NoDisposeClass
        Public newFile As FileStream
    End Class

    Public Class CreationClass
        Public Sub Create()
            Dim obj As NoDisposeClass = New NoDisposeClass()
            obj.newFile = New FileStream(""data.txt"", FileMode.Append)
        End Sub
    End Class
");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO

    Public Class NoDisposeClass
        Public newFile As FileStream
        Public Sub New(fs As FileStream)
            newFile = fs
        End Sub
    End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithNoDisposeMethodInScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
   Imports System.IO

   ' This class violates the rule.
   Public Class [|NoDisposeMethod|]

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream("""", FileMode.Append)
      End Sub

   End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithNoDisposeMethodOutOfScope()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
   Imports System.IO

   ' This class violates the rule.
   Public Class [|NoDisposeMethod|]

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

   End Class

   Public Class SomeClass
   End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithADisposeMethod()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
   Imports System
   Imports System.IO

   ' This class satisfies the rule.
   Public Class HasDisposeMethod
      Implements IDisposable

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

      Overloads Protected Overridable Sub Dispose(disposing As Boolean)

         If disposing Then
            ' dispose managed resources
            newFile.Close()
         End If

         ' free native resources 

      End Sub 'Dispose


      Overloads Public Sub Dispose() Implements IDisposable.Dispose

         Dispose(True)
         GC.SuppressFinalize(Me)

      End Sub 'Dispose

   End Class
");
        }

        [Fact, WorkItem(1562, "https://github.com/dotnet/roslyn-analyzers/issues/1562")]
        public async Task CA1001BasicTestWithIDisposableField()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO

Namespace ClassLibrary1
    Class Class1
        Private Readonly _disp1 As IDisposable = new MemoryStream()
    End Class
End Namespace
",
            GetCA1001BasicResultAt(6, 11, "Class1", "_disp1"));
        }

        [Fact, WorkItem(3905, "https://github.com/dotnet/roslyn-analyzers/issues/3905")]
        public async Task CA1001_OnlyListDisposableFields()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

public class {|#0:NoDisposeClass|}
{
    FileStream _fs1;
    FileStream _fs2;

    public NoDisposeClass(FileStream fs)
    {
        _fs1 = new FileStream(""data.txt"", FileMode.Append);
        _fs2 = fs;
    }
}
",
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO

Public Class {|#0:NoDisposeClass|}
    Private _fs1 As FileStream
    Private _fs2 As FileStream

    Public Sub New(ByVal fs As FileStream)
        _fs1 = New FileStream(""data.txt"", FileMode.Append)
        _fs2 = fs
    End Sub
End Class
",
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1"));
        }

        [Fact, WorkItem(3905, "https://github.com/dotnet/roslyn-analyzers/issues/3905")]
        public async Task CA1001_ListDisposableFields()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System.IO;

public class {|#0:NoDisposeClass|}
{
    FileStream _fs1 = new FileStream(""data.txt"", FileMode.Append), _fs2 = new FileStream(""data.txt"", FileMode.Append);
    FileStream _fs3;
    FileStream _fs4;

    public NoDisposeClass()
    {
        _fs3 = new FileStream(""data.txt"", FileMode.Append);
        _fs4 = new FileStream(""data.txt"", FileMode.Append);
    }
}
",
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1', '_fs2', '_fs3', '_fs4"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO

Public Class {|#0:NoDisposeClass|}
    Private _fs1 As FileStream = new FileStream(""data.txt"", FileMode.Append), _fs2 As FileStream = new FileStream(""data.txt"", FileMode.Append)
    Private _fs3 As FileStream
    Private _fs4 As FileStream

    Public Sub New(ByVal fs As FileStream)
        _fs3 = new FileStream(""data.txt"", FileMode.Append)
        _fs4 = new FileStream(""data.txt"", FileMode.Append)
    End Sub
End Class
",
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1', '_fs2', '_fs3', '_fs4"));
        }

        [Theory, WorkItem(3905, "https://github.com/dotnet/roslyn-analyzers/issues/3905")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = FileStream")]
        [InlineData("dotnet_code_quality.CA1001.excluded_symbol_names = FileStream")]
        [InlineData("dotnet_code_quality.CA1001.excluded_symbol_names = T:System.IO.FileStream")]
        [InlineData("dotnet_code_quality.CA1001.excluded_symbol_names = FileStr*")]
        public async Task CA1001_ExcludedSymbolNames(string editorConfigText)
        {
            var args = editorConfigText.Length == 0 ? "_fs', '_ms" : "_ms";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
using System.IO;

public class {|#0:SomeClass|}
{
    private FileStream _fs = new FileStream(""data.txt"", FileMode.Append);
    private MemoryStream _ms = new MemoryStream();
}
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                    ExpectedDiagnostics = { VerifyCS.Diagnostic().WithLocation(0).WithArguments("SomeClass", args), },
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
Imports System.IO

Public Class {|#0:SomeClass|}
    Private _fs As FileStream = new FileStream(""data.txt"", FileMode.Append)
    Private _ms As MemoryStream = new MemoryStream()
End Class
",
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), },
                    ExpectedDiagnostics = { VerifyVB.Diagnostic().WithLocation(0).WithArguments("SomeClass", args), },
                },
            }.RunAsync();
        }

        private static DiagnosticResult GetCA1001CSharpResultAt(int line, int column, string objectName, string disposableFields)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName, disposableFields);

        private static DiagnosticResult GetCA1001BasicResultAt(int line, int column, string objectName, string disposableFields)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not used banned APIs
                .WithArguments(objectName, disposableFields);
    }
}
