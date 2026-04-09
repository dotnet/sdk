// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.TypesThatOwnDisposableFieldsShouldBeDisposableFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzerTests
    {
        [Fact]
        public async Task CA1001CSharpTestWithNoDisposableTypeAsync()
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
        public async Task CA1001CSharpTestWithNoCreationOfDisposableObjectAsync()
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
        public async Task CA1001CSharpTestWithFieldInitAndNoDisposeMethodAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System.IO;

    public class [|NoDisposeClass|]
    {
        FileStream newFile1, newFile2 = new FileStream(""data.txt"", FileMode.Append);
    }
", @"
using System.IO;

    public class NoDisposeClass : System.IDisposable
{
        FileStream newFile1, newFile2 = new FileStream(""data.txt"", FileMode.Append);

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        [WorkItem(5834, "https://github.com/dotnet/roslyn-analyzers/issues/5834")]
        public async Task CA1001CSharpTestWithFieldInitAndNoDisposeMethod_TargetTypedNewAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System.IO;

public class [|NoDisposeClass|]
{
    FileStream newFile1, newFile2 = new(""data.txt"", FileMode.Append);
}
",
                FixedCode = @"
using System.IO;

public class NoDisposeClass : System.IDisposable
{
    FileStream newFile1, newFile2 = new(""data.txt"", FileMode.Append);

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1001CSharpTestWithCtorInitAndNoDisposeMethodAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
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
", @"
using System.IO;

    // This class violates the rule.
    public class NoDisposeClass : System.IDisposable
{
        FileStream newFile;

        public NoDisposeClass()
        {
            newFile = new FileStream(""data.txt"", FileMode.Append);
        }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CA1001CSharpTestWithCreationOfDisposableObjectInOtherClassAsync()
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
        public async Task CA1001CSharpTestWithNoDisposeMethodInScopeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
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
", @"
using System.IO;

    // This class violates the rule.
    public class NoDisposeClass : System.IDisposable
{
        FileStream newFile;

        public NoDisposeClass()
        {
            newFile = new FileStream(""data.txt"", FileMode.Append);
        }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
");
        }

        [Fact]
        [WorkItem(5834, "https://github.com/dotnet/roslyn-analyzers/issues/5834")]
        public async Task CA1001CSharpTestWithNoDisposeMethodInScope_TargetTypedNewAsync()
        {
            await new VerifyCS.Test
            {
                TestCode = @"
using System.IO;

// This class violates the rule.
public class [|NoDisposeClass|]
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new(""data.txt"", FileMode.Append);
    }
}
",
                FixedCode = @"
using System.IO;

// This class violates the rule.
public class NoDisposeClass : System.IDisposable
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new(""data.txt"", FileMode.Append);
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
",
                LanguageVersion = CodeAnalysis.CSharp.LanguageVersion.CSharp10,
            }.RunAsync();
        }

        [Fact]
        public async Task CA1001CSharpScopedTestWithNoDisposeMethodOutOfScopeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
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
", @"
using System;
using System.IO;

// This class violates the rule.
public class NoDisposeClass : IDisposable
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new FileStream(""data.txt"", FileMode.Append);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}

public class SomeClass
{
}
");
        }

        [Fact]
        public async Task CA1001CSharpTestWithADisposeMethodAsync()
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
        public async Task CA1001CSharpTestWithIDisposableFieldAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

namespace ClassLibrary1
{
    public class [|Class1|]
    {
        private readonly IDisposable _disp1 = new MemoryStream();
    }
}
", @"
using System;
using System.IO;

namespace ClassLibrary1
{
    public class Class1 : IDisposable
    {
        private readonly IDisposable _disp1 = new MemoryStream();

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
");
        }

        [Fact, WorkItem(1562, "https://github.com/dotnet/roslyn-analyzers/issues/1562")]
        public async Task CA1001CSharpTestWithIAsyncDisposableAsync()
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
        public async Task CA1001BasicTestWithNoDisposableTypeAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Module Module1

    Sub Main()

    End Sub

End Module
");
        }

        [Fact]
        public async Task CA1001BasicTestWithNoCreationOfDisposableObjectAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System.IO

    Public Class NoDisposeClass
	    Dim newFile As FileStream
    End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithFieldInitAndNoDisposeMethodAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.IO
           
   ' This class violates the rule. 
    Public Class [|NoDisposeClass|]
        Dim newFile As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
", @"
Imports System.IO
           
   ' This class violates the rule. 
    Public Class NoDisposeClass
    Implements System.IDisposable

    Dim newFile As FileStream = New FileStream(""data.txt"", FileMode.Append)

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.IO
      
   ' This class violates the rule. 
    Public Class [|NoDisposeClass|]
        Dim newFile1 As FileStream, newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
", @"
Imports System.IO
      
   ' This class violates the rule. 
    Public Class NoDisposeClass
    Implements System.IDisposable

    Dim newFile1 As FileStream, newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
");

            await VerifyVB.VerifyCodeFixAsync(@"
Imports System.IO
    
   ' This class violates the rule. 
    Public Class [|NoDisposeClass|]
        Dim newFile1 As FileStream
        Dim newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
", @"
Imports System.IO
    
   ' This class violates the rule. 
    Public Class NoDisposeClass
    Implements System.IDisposable

    Dim newFile1 As FileStream
        Dim newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithCtorInitAndNoDisposeMethodAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
   Imports System
   Imports System.IO

   ' This class violates the rule. 
   Public Class [|NoDisposeMethod|]

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

   End Class
", @"
   Imports System
   Imports System.IO

   ' This class violates the rule. 
   Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New NotImplementedException()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithCreationOfDisposableObjectInOtherClassAsync()
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
        public async Task CA1001BasicTestWithNoDisposeMethodInScopeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
   Imports System.IO

   ' This class violates the rule.
   Public Class [|NoDisposeMethod|]

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream("""", FileMode.Append)
      End Sub

   End Class
", @"
   Imports System.IO

   ' This class violates the rule.
   Public Class NoDisposeMethod
    Implements System.IDisposable

    Dim newFile As FileStream

      Sub New()
         newFile = New FileStream("""", FileMode.Append)
      End Sub

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithNoDisposeMethodOutOfScopeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
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
", @"
   Imports System.IO

   ' This class violates the rule.
   Public Class NoDisposeMethod
    Implements System.IDisposable

    Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class

   Public Class SomeClass
   End Class
");
        }

        [Fact]
        public async Task CA1001BasicTestWithADisposeMethodAsync()
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
        public async Task CA1001BasicTestWithIDisposableFieldAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

Namespace ClassLibrary1
    Class [|Class1|]
        Private Readonly _disp1 As IDisposable = new MemoryStream()
    End Class
End Namespace
", @"
Imports System
Imports System.IO

Namespace ClassLibrary1
    Class Class1
        Implements IDisposable

        Private Readonly _disp1 As IDisposable = new MemoryStream()

        Public Sub Dispose() Implements IDisposable.Dispose
            Throw New NotImplementedException()
        End Sub
    End Class
End Namespace
");
        }

        [Fact, WorkItem(3905, "https://github.com/dotnet/roslyn-analyzers/issues/3905")]
        public async Task CA1001_OnlyListDisposableFieldsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
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
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1"), @"
using System.IO;

public class NoDisposeClass : System.IDisposable
{
    FileStream _fs1;
    FileStream _fs2;

    public NoDisposeClass(FileStream fs)
    {
        _fs1 = new FileStream(""data.txt"", FileMode.Append);
        _fs2 = fs;
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
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
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1"), @"
Imports System.IO

Public Class NoDisposeClass
    Implements System.IDisposable

    Private _fs1 As FileStream
    Private _fs2 As FileStream

    Public Sub New(ByVal fs As FileStream)
        _fs1 = New FileStream(""data.txt"", FileMode.Append)
        _fs2 = fs
    End Sub

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
");
        }

        [Fact, WorkItem(3905, "https://github.com/dotnet/roslyn-analyzers/issues/3905")]
        public async Task CA1001_ListDisposableFieldsAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
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
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1', '_fs2', '_fs3', '_fs4"), @"
using System.IO;

public class NoDisposeClass : System.IDisposable
{
    FileStream _fs1 = new FileStream(""data.txt"", FileMode.Append), _fs2 = new FileStream(""data.txt"", FileMode.Append);
    FileStream _fs3;
    FileStream _fs4;

    public NoDisposeClass()
    {
        _fs3 = new FileStream(""data.txt"", FileMode.Append);
        _fs4 = new FileStream(""data.txt"", FileMode.Append);
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
");

            await VerifyVB.VerifyCodeFixAsync(@"
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
            VerifyVB.Diagnostic().WithLocation(0).WithArguments("NoDisposeClass", "_fs1', '_fs2', '_fs3', '_fs4"), @"
Imports System.IO

Public Class NoDisposeClass
    Implements System.IDisposable

    Private _fs1 As FileStream = new FileStream(""data.txt"", FileMode.Append), _fs2 As FileStream = new FileStream(""data.txt"", FileMode.Append)
    Private _fs3 As FileStream
    Private _fs4 As FileStream

    Public Sub New(ByVal fs As FileStream)
        _fs3 = new FileStream(""data.txt"", FileMode.Append)
        _fs4 = new FileStream(""data.txt"", FileMode.Append)
    End Sub

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
");
        }

        [Theory, WorkItem(3905, "https://github.com/dotnet/roslyn-analyzers/issues/3905")]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = FileStream")]
        [InlineData("dotnet_code_quality.CA1001.excluded_symbol_names = FileStream")]
        [InlineData("dotnet_code_quality.CA1001.excluded_symbol_names = T:System.IO.FileStream")]
        [InlineData("dotnet_code_quality.CA1001.excluded_symbol_names = FileStr*")]
        public async Task CA1001_ExcludedSymbolNamesAsync(string editorConfigText)
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
                FixedCode = @"
using System.IO;

public class SomeClass : System.IDisposable
{
    private FileStream _fs = new FileStream(""data.txt"", FileMode.Append);
    private MemoryStream _ms = new MemoryStream();

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }
}
",
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
                FixedCode = @"
Imports System.IO

Public Class SomeClass
    Implements System.IDisposable

    Private _fs As FileStream = new FileStream(""data.txt"", FileMode.Append)
    Private _ms As MemoryStream = new MemoryStream()

    Public Sub Dispose() Implements System.IDisposable.Dispose
        Throw New System.NotImplementedException()
    End Sub
End Class
",
            }.RunAsync();
        }

        [Fact]
        public async Task CA1001CSharpCodeFixNoDisposeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

// This class violates the rule.
public class [|NoDisposeClass|]
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new FileStream("""", FileMode.Append);
    }
}
",
@"
using System;
using System.IO;

// This class violates the rule.
public class NoDisposeClass : IDisposable
{
    FileStream newFile;

    public NoDisposeClass()
    {
        newFile = new FileStream("""", FileMode.Append);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CA1001BasicCodeFixNoDisposeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class [|NoDisposeMethod|]

    Dim newFile As FileStream

    Sub New()
        newFile = New FileStream("""", FileMode.Append)
    End Sub

End Class
",
@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream

    Sub New()
        newFile = New FileStream("""", FileMode.Append)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New NotImplementedException()
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001CSharpCodeFixHasDisposeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

// This class violates the rule.
public class [|NoDisposeClass|]
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    void Dispose() {
// Some content
}
}
",
@"
using System;
using System.IO;

// This class violates the rule.
public class NoDisposeClass : IDisposable
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    public void Dispose() {
// Some content
}
}
");
        }

        [Fact]
        public async Task CA1001CSharpCodeFixHasWrongDisposeAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
using System;
using System.IO;

// This class violates the rule.
public partial class [|NoDisposeClass|]
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    void Dispose(int x) {
// Some content
}
}
",
@"
using System;
using System.IO;

// This class violates the rule.
public partial class NoDisposeClass : IDisposable
{
    FileStream newFile = new FileStream("""", FileMode.Append);

    void Dispose(int x) {
// Some content
}

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
");
        }

        [Fact]
        public async Task CA1001BasicCodeFixHasDisposeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class [|NoDisposeMethod|]

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Sub Dispose()

    End Sub
End Class
",
@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Public Sub Dispose() Implements IDisposable.Dispose
    End Sub
End Class
");
        }

        [Fact]
        public async Task CA1001BasicCodeFixHasWrongDisposeAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class [|NoDisposeMethod|]

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Sub Dispose(x As Integer)
    End Sub
End Class
",
@"
Imports System
Imports System.IO

' This class violates the rule. 
Public Class NoDisposeMethod
    Implements IDisposable

    Dim newFile As FileStream = New FileStream("""", FileMode.Append)

    Sub Dispose(x As Integer)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Throw New NotImplementedException()
    End Sub
End Class
");
        }
    }
}
