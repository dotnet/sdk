// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public partial class TypesThatOwnDisposableFieldsShouldBeDisposableAnalyzerTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer();
        }

        [Fact]
        public void CA1001CSharpTestWithNoDisposableType()
        {
            VerifyCSharp(@"
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
");
        }

        [Fact]
        public void CA1001CSharpTestWithNoCreationOfDisposableObject()
        {
            VerifyCSharp(@"
using System.IO;

    public class NoDisposeClass
    {
        FileStream newFile;
    }
");
        }

        [Fact]
        public void CA1001CSharpTestWithFieldInitAndNoDisposeMethod()
        {
            VerifyCSharp(@"
using System.IO;

    public class NoDisposeClass
    {
        FileStream newFile1, newFile2 = new FileStream(""data.txt"", FileMode.Append);
    }
",
            GetCA1001CSharpResultAt(4, 18, "NoDisposeClass", "newFile1, newFile2"));
        }

        [Fact]
        public void CA1001CSharpTestWithCtorInitAndNoDisposeMethod()
        {
            VerifyCSharp(@"
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
        public void CA1001CSharpTestWithCreationOfDisposableObjectInOtherClass()
        {
            VerifyCSharp(@"
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

            VerifyCSharp(@"
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
        public void CA1001CSharpTestWithNoDisposeMethodInScope()
        {
            VerifyCSharp(@"
using System.IO;

    // This class violates the rule.
    [|public class NoDisposeClass
    {
        FileStream newFile;

        public NoDisposeClass()
        {
            newFile = new FileStream(""data.txt"", FileMode.Append);
        }
    }|]
",
            GetCA1001CSharpResultAt(5, 18, "NoDisposeClass", "newFile"));
        }

        [Fact]
        public void CA1001CSharpScopedTestWithNoDisposeMethodOutOfScope()
        {
            VerifyCSharp(@"
using System;
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
   
[|public class Foo
{
}
|]
");
        }

        [Fact]
        public void CA1001CSharpTestWithADisposeMethod()
        {
            VerifyCSharp(@"
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
        public void CA1001CSharpTestWithIDisposableField()
        {
            VerifyCSharp(@"
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

        [Fact]
        public void CA1001BasicTestWithNoDisposableType()
        {
            VerifyBasic(@"
Module Module1

    Sub Main()

    End Sub

End Module
");
        }

        [Fact]
        public void CA1001BasicTestWithNoCreationOfDisposableObject()
        {
            VerifyBasic(@"
Imports System.IO

    Public Class NoDisposeClass
	    Dim newFile As FileStream
    End Class
");
        }

        [Fact]
        public void CA1001BasicTestWithFieldInitAndNoDisposeMethod()
        {
            VerifyBasic(@"
Imports System.IO
           
   ' This class violates the rule. 
    Public Class NoDisposeClass
        Dim newFile As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
",
            GetCA1001BasicResultAt(5, 18, "NoDisposeClass", "newFile"));

            VerifyBasic(@"
Imports System.IO
      
   ' This class violates the rule. 
    Public Class NoDisposeClass
        Dim newFile1 As FileStream, newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
",
            GetCA1001BasicResultAt(5, 18, "NoDisposeClass", "newFile1, newFile2"));

            VerifyBasic(@"
Imports System.IO
    
   ' This class violates the rule. 
    Public Class NoDisposeClass
        Dim newFile1 As FileStream
        Dim newFile2 As FileStream = New FileStream(""data.txt"", FileMode.Append)
    End Class
",
            GetCA1001BasicResultAt(5, 18, "NoDisposeClass", "newFile1, newFile2"));
        }

        [Fact]
        public void CA1001BasicTestWithCtorInitAndNoDisposeMethod()
        {
            VerifyBasic(@"
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
        public void CA1001BasicTestWithCreationOfDisposableObjectInOtherClass()
        {
            VerifyBasic(@"
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

            VerifyBasic(@"
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
        public void CA1001BasicTestWithNoDisposeMethodInScope()
        {
            VerifyBasic(@"
   Imports System.IO

   ' This class violates the rule. 
   [|Public Class NoDisposeMethod

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream("""", FileMode.Append)
      End Sub

   End Class|]
",
            GetCA1001BasicResultAt(5, 17, "NoDisposeMethod", "newFile"));
        }

        [Fact]
        public void CA1001BasicTestWithNoDisposeMethodOutOfScope()
        {
            VerifyBasic(@"
   Imports System.IO

   ' This class violates the rule. 
   Public Class NoDisposeMethod

      Dim newFile As FileStream

      Sub New()
         newFile = New FileStream(Nothing, FileMode.Open)
      End Sub

   End Class

   [|
   Public Class Foo
   End Class
   |]
");
        }

        [Fact]
        public void CA1001BasicTestWithADisposeMethod()
        {
            VerifyBasic(@"
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
        public void CA1001BasicTestWithIDisposableField()
        {
            VerifyBasic(@"
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

        private static DiagnosticResult GetCA1001CSharpResultAt(int line, int column, string objectName, string disposableFields)
        {
            return GetCSharpResultAt(line, column, CSharpTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.RuleId,
                string.Format(MicrosoftCodeQualityAnalyzersResources.TypesThatOwnDisposableFieldsShouldBeDisposableMessageNonBreaking, objectName, disposableFields));
        }

        private static DiagnosticResult GetCA1001BasicResultAt(int line, int column, string objectName, string disposableFields)
        {
            return GetBasicResultAt(line, column, BasicTypesThatOwnDisposableFieldsShouldBeDisposableAnalyzer.RuleId,
                string.Format(MicrosoftCodeQualityAnalyzersResources.TypesThatOwnDisposableFieldsShouldBeDisposableMessageNonBreaking, objectName, disposableFields));
        }
    }
}
