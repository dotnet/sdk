﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotCallDangerousMethodsInDeserialization,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Security.DoNotCallDangerousMethodsInDeserialization,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotCallDangerousMethodsInDeserializationTests
    {
#if NETCOREAPP
        private const string NullableSuffixOnNetCoreApp = "?";
#else
        private const string NullableSuffixOnNetCoreApp = "";
#endif

        [Fact]
        public async Task TestOnDeserializingDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserializing()]
    internal void OnDeserializingMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}",
            GetCSharpResultAt(
                12,
                19,
                "TestClass",
                "OnDeserializingMethod",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserializing()>
        Sub OnDeserializingMethod(ByVal context As StreamingContext)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                13,
                "TestClass",
                "OnDeserializingMethod",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnDeserializedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}",
            GetCSharpResultAt(
                12,
                19,
                "TestClass",
                "OnDeserializedMethod",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserialized()>
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                13,
                "TestClass",
                "OnDeserializedMethod",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnMultiAttributesDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserialized()]
    [OnSerialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}",
            GetCSharpResultAt(
                13,
                19,
                "TestClass",
                "OnDeserializedMethod",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserialized()>
        <OnSerialized()>
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                13,
                "TestClass",
                "OnDeserializedMethod",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnDeserializedMediateInvocationDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        var obj = new TestClass();
        obj.TestMethod();
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }

    private void TestMethod()
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}",
            GetCSharpResultAt(
                12,
                19,
                "TestClass",
                "OnDeserializedMethod",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserialized()>
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim obj As New TestClass()
            obj.TestMethod()
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub

        Sub TestMethod()
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                13,
                "TestClass",
                "OnDeserializedMethod",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnDeserializedMultiMediateInvocationsDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        var obj = new TestClass();
        var count = 2;
        obj.TestMethod(count);
    }
    
    private void TestMethod(int count)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);

        if(count != 0)
        {
            var obj = new TestClass();
            obj.TestMethod(--count);
        }
    }
}",
            GetCSharpResultAt(
                12,
                19,
                "TestClass",
                "OnDeserializedMethod",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserialized()>
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim obj As New TestClass()
            obj.TestMethod(2)
        End Sub

        Sub TestMethod(ByVal count As Integer)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)

            If count <> 0
                Dim obj As New TestClass()
                count = count - 1
                obj.TestMethod(count)
            End If
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                13,
                "TestClass",
                "OnDeserializedMethod",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnDeserializationImplicitlyDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;
    
    public void OnDeserialization(Object sender)
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}",
            GetCSharpResultAt(
                13,
                17,
                "TestClass",
                "OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestOnDeserializationWriteAllBytesDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserializationExplictlyImplemented(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserializationExplictlyImplemented",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnDeserializationWriteAllLinesDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        var strings = new string[]{""111"", ""222""};
        File.WriteAllLines(path, strings, Encoding.ASCII);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllLines(string path, string[] contents, Encoding encoding)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Text

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            Dim strings(9) As String
            File.WriteAllLines(path, strings, Encoding.ASCII)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.WriteAllLines(path As String, contents As String(), encoding As Encoding)"));
        }

        [Fact]
        public async Task TestOnDeserializationWriteAllTextDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        var contents = ""This is the contents."";
        File.WriteAllText(path, contents);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllText(string path, string contents)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            Dim contents As String
            contents = ""This is the contents.""
            File.WriteAllText(path, contents)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.WriteAllText(path As String, contents As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationCopyDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var sourceFileName = ""source file"";
        var destFileName = ""dest file"";
        File.Copy(sourceFileName, destFileName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.Copy(string sourceFileName, string destFileName)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim sourceFileName As String
            sourceFileName = ""source file""
            Dim destFileName As String
            destFileName = ""dest file""
            File.Copy(sourceFileName, destFileName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.Copy(sourceFileName As String, destFileName As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationMoveDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var sourceFileName = ""source file"";
        var destFileName = ""dest file"";
        File.Move(sourceFileName, destFileName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.Move(string sourceFileName, string destFileName)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim sourceFileName As String
            sourceFileName = ""source file""
            Dim destFileName As String
            destFileName = ""dest file""
            Dim bytes(9) As Byte
            File.Move(sourceFileName, destFileName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.Move(sourceFileName As String, destFileName As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationAppendAllLinesDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        var strings = new string[]{""111"", ""222""};
        File.AppendAllLines(path, strings);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.AppendAllLines(string path, IEnumerable<string> contents)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            Dim strings(9) As String
            File.AppendAllLines(""C:\\"", strings)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.AppendAllLines(path As String, contents As IEnumerable(Of String))"));
        }

        [Fact]
        public async Task TestOnDeserializationAppendAllTextDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        var contents = ""This is the contents."";
        File.AppendAllText(path, contents);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.AppendAllText(string path, string contents)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            Dim contents As String
            File.AppendAllText(path, contents)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.AppendAllText(path As String, contents As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationAppendTextDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        File.AppendText(path);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "StreamWriter File.AppendText(string path)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            File.AppendText(path)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Function File.AppendText(path As String) As StreamWriter"));
        }

        [Fact]
        public async Task TestOnDeserializationDeleteDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        File.Delete(path);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.Delete(string path)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            File.Delete(path)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub File.Delete(path As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationDeleteOfDirectoryDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        Directory.Delete(path);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void Directory.Delete(string path)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            Directory.Delete(path)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub Directory.Delete(path As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationDeleteOfFileInfoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        new FileInfo(""fileName"").Delete();
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void FileInfo.Delete()"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim fileInfo As New FileInfo(""fileName"")
            fileInfo.Delete()
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub FileInfo.Delete()"));
        }

        [Fact]
        public async Task TestOnDeserializationDeleteOfDirectoryInfoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        new DirectoryInfo(""path"").Delete();
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void DirectoryInfo.Delete()"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim directoryInfo As new DirectoryInfo(""path"")
            directoryInfo.Delete()
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                12,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub DirectoryInfo.Delete()"));
        }

        [Fact]
        public async Task TestOnDeserializationDeleteOfLogStoreDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.IO.Log;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace System.IO.Log
{
    public sealed class LogStore : IDisposable
    {
        public static void Delete (string path)
        {
        }
        
        public void Dispose ()
        {
        }
    }
}

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var path = ""C:\\"";
        LogStore.Delete(path);
    }
}",
            GetCSharpResultAt(
                28,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void LogStore.Delete(string path)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.IO.Log
Imports System.Runtime.Serialization

Namespace System.IO.Log
    Public NotInheritable Class LogStore
        Implements IDisposable
        Public Shared Sub Delete (path As String)
        End Sub
        
        Public Sub Dispose () Implements IDisposable.Dispose
        End Sub
    End Class
End Namespace

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim path As String
            path = ""C:\\""
            LogStore.Delete(path)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                24,
                20,
                "TestClass",
                "OnDeserialization",
                "Sub LogStore.Delete(path As String)"));
        }

        [Fact]
        public async Task TestOnDeserializationGetLoadedModulesDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var assem = typeof(TestClass).Assembly;
        var modules = assem.GetLoadedModules();
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Module[] Assembly.GetLoadedModules()"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim assem As Assembly = GetType(TestClass).Assembly
            assem.GetLoadedModules()
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.GetLoadedModules() As [Module]()"));
        }

        [Fact]
        public async Task TestOnDeserializationLoadDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var fullName = ""sysglobl, Version = 4.0.0.0, Culture = neutral, "" +
                       ""PublicKeyToken=b03f5f7f11d50a3a, processor architecture=MSIL"";
        var an = new AssemblyName(fullName);
        var assem = Assembly.Load(an);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Assembly Assembly.Load(AssemblyName assemblyRef)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim fullName As String
            fullName = ""sysglobl, Version = 4.0.0.0, Culture = neutral, _
                       PublicKeyToken=b03f5f7f11d50a3a, processor architecture=MSIL""
            Dim an As new AssemblyName(fullName)
            Assembly.Load(an)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.Load(assemblyRef As AssemblyName) As Assembly"));
        }

        [Fact]
        public async Task TestOnDeserializationLoadFileDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var fileName = ""C:\\test.txt"";
        var assem = Assembly.LoadFile(fileName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Assembly Assembly.LoadFile(string path)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim fileName As String
            fileName = ""C:\\test.txt""
            Assembly.LoadFile(fileName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.LoadFile(path As String) As Assembly"));
        }

        [Fact]
        public async Task TestOnDeserializationLoadFromDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var assemblyName = ""assembly file"";
        var assem = Assembly.LoadFrom(assemblyName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Assembly Assembly.LoadFrom(string assemblyFile)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim assemblyName As String
            assemblyName = ""assembly file""
            Assembly.LoadFrom(assemblyName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.LoadFrom(assemblyFile As String) As Assembly"));
        }

        [Fact]
        public async Task TestOnDeserializationLoadModuleDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        Assembly assem = typeof(TestClass).Assembly;
        var moduleName = ""module name"";
        var rawModule = new byte[] {0x20, 0x20, 0x20};
        var module = assem.LoadModule(moduleName, rawModule);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                $"Module Assembly.LoadModule(string moduleName, byte[]{NullableSuffixOnNetCoreApp} rawModule)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim assem As Assembly = GetType(TestClass).Assembly
            Dim moduleName As String
            moduleName = ""module name""
            Dim rawModule(9) As Byte
            assem.LoadModule(moduleName, rawModule)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.LoadModule(moduleName As String, rawModule As Byte()) As [Module]"));
        }

        [Fact]
        public async Task TestOnDeserializationLoadWithPartialNameDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var partialName = ""partial name"";
        var assem = Assembly.LoadWithPartialName(partialName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass", "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                $"Assembly{NullableSuffixOnNetCoreApp} Assembly.LoadWithPartialName(string partialName)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim partialName As String
            partialName = ""partial name""
            Assembly.LoadWithPartialName(partialName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.LoadWithPartialName(partialName As String) As Assembly"));
        }

        [Fact]
        public async Task TestOnDeserializationReflectionOnlyLoadDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var rawAssembly = new byte[] {0x20, 0x20, 0x20};
        var assem = Assembly.ReflectionOnlyLoad(rawAssembly);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Assembly Assembly.ReflectionOnlyLoad(byte[] rawAssembly)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim rawAssembly(9) As Byte
            Assembly.ReflectionOnlyLoad(rawAssembly)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.ReflectionOnlyLoad(rawAssembly As Byte()) As Assembly"));
        }

        [Fact]
        public async Task TestOnDeserializationReflectionOnlyLoadFromDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var assemblyName = ""assembly file"";
        var assem = Assembly.ReflectionOnlyLoadFrom(assemblyName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Assembly Assembly.ReflectionOnlyLoadFrom(string assemblyFile)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim assemblyName As String
            assemblyName = ""assembly file""
            Assembly.ReflectionOnlyLoadFrom(assemblyName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.ReflectionOnlyLoadFrom(assemblyFile As String) As Assembly"));
        }

        [Fact]
        public async Task TestOnDeserializationUnsafeLoadFromDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        var assemblyName = ""assembly file"";
        var assem = Assembly.UnsafeLoadFrom(assemblyName);
    }
}",
            GetCSharpResultAt(
                13,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "Assembly Assembly.UnsafeLoadFrom(string assemblyFile)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Public Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim assemblyName As String
            assemblyName = ""assembly file""
            Assembly.UnsafeLoadFrom(assemblyName)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                13,
                20,
                "TestClass",
                "OnDeserialization",
                "Function Assembly.UnsafeLoadFrom(assemblyFile As String) As Assembly"));
        }

        [Fact]
        public async Task TestUsingGenericwithTypeSpecifiedDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

[Serializable()]
public class TestGenericClass<T>
{
    private T memberInGeneric;

    public void TestGenericMethod()
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}

[Serializable()]
public class TestClass : IDeserializationCallback
{
    private TestGenericClass<int> member;

    void IDeserializationCallback.OnDeserialization(Object sender)
    {
        member.TestGenericMethod();
    }
}",
            GetCSharpResultAt(
                26,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestUsingInterfaceDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

interface TestInterface
{
    void TestInterfaceMethod();
}

[Serializable()]
public class TestInterfaceImplement : TestInterface
{
    public void TestInterfaceMethod()
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}

[Serializable()]
public class TestClass : IDeserializationCallback
{
    private TestInterfaceImplement member;

    void IDeserializationCallback.OnDeserialization(Object sender)
    {
        member.TestInterfaceMethod();
    }
}",
            GetCSharpResultAt(
                29,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestStaticDelegateFieldDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

public delegate void TestDelegate();

[Serializable()]
public class TestAnotherClass
{
    public static TestDelegate staticDelegateField = () =>
    {
        var path = ""C:\\"";
        var bytes = new byte[] { 0x20, 0x20, 0x20 };
        File.WriteAllBytes(path, bytes);
    };
}

[Serializable()]
public class TestClass : IDeserializationCallback
{
    void IDeserializationCallback.OnDeserialization(Object sender)
    {
        TestAnotherClass.staticDelegateField();
    }
}",
            GetCSharpResultAt(
                24,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestDelegateFieldDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

public delegate void TestDelegate();

[Serializable()]
public class TestAnotherClass
{
    public TestDelegate delegateField;
}

[Serializable()]
public class TestClass : IDeserializationCallback 
{
    void IDeserializationCallback.OnDeserialization(Object sender) 
    {
        TestAnotherClass testAnotherClass = new TestAnotherClass();
        testAnotherClass.delegateField = () =>
        {
            var path = ""C:\\"";
            var bytes = new byte[] { 0x20, 0x20, 0x20 };
            File.WriteAllBytes(path, bytes);
        };
        testAnotherClass.delegateField();
    }
}",
            GetCSharpResultAt(
                19,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestUsingAbstractClassDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

public abstract class TestAbstractClass
{
    public abstract void TestAbstractMethod();
}

[Serializable()]
public class TestDerivedClass : TestAbstractClass
{
    public override void TestAbstractMethod()
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}

[Serializable()]
public class TestClass : IDeserializationCallback
{
    private TestDerivedClass member;

    void IDeserializationCallback.OnDeserialization(Object sender)
    {
        member.TestAbstractMethod();
    }
}",
            GetCSharpResultAt(
                29,
                35,
                "TestClass",
                "System.Runtime.Serialization.IDeserializationCallback.OnDeserialization",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestFinalizeDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    ~TestClass()
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}",
            GetCSharpResultAt(
                11,
                6,
                "TestClass",
                "Finalize",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        Protected Overrides Sub Finalize()
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                11,
                33,
                "TestClass",
                "Finalize",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestDisposeDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass : IDisposable
{
    private string member;
    bool disposed = false;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            byte[] bytes = new byte[] { 0x20, 0x20, 0x20 };
            File.WriteAllBytes(""C:\\"", bytes);
        }

        disposed = true;
    }

    ~TestClass()
    {
        Dispose(false);
    }
}",
            GetCSharpResultAt(
                13,
                17,
                "TestClass",
                "Dispose",
                "void File.WriteAllBytes(string path, byte[] bytes)"),
            GetCSharpResultAt(
                35,
                6,
                "TestClass",
                "Finalize",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDisposable
        Private member As String
        Protected disposed As Boolean = False

        Protected Overridable Sub Dispose(ByVal disposing As Boolean)
            If Not Me.disposed Then
                If disposing Then
                    Dim bytes(9) As Byte
                    File.WriteAllBytes(""C:\\"", bytes)
                End If
            End If
            Me.disposed = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overrides Sub Finalize()
            Dispose(False)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                23,
                20,
                "TestClass",
                "Dispose",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"),
            GetBasicResultAt(28,
                33,
                "TestClass",
                "Finalize",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestFinalizeWhenSubClassWithSerializableDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    ~TestClass()
    {
    }
}

[Serializable()]
public class SubTestClass : TestClass
{
    private string member;

    ~SubTestClass()
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}",
            GetCSharpResultAt(
                21,
                6,
                "SubTestClass",
                "Finalize",
                "void File.WriteAllBytes(string path, byte[] bytes)"));

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        Protected Overrides Sub Finalize()
        End Sub
    End Class

    <Serializable()> _
    Class SubTestClass 
        Inherits TestClass
        Private member As String
        
        Protected Overrides Sub Finalize()
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace",
            GetBasicResultAt(
                20,
                33,
                "SubTestClass",
                "Finalize",
                "Sub File.WriteAllBytes(path As String, bytes As Byte())"));
        }

        [Fact]
        public async Task TestOnDeserializingNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserializing()]
    internal void OnDeserializingMethod(StreamingContext context)
    {
        var obj = new TestClass();
        obj.TestMethod();
    }
    
    private void TestMethod()
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserializing()>
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim obj As New TestClass()
            obj.TestMethod()
        End Sub

        Sub TestMethod()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestOnDeserializedNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        var obj = new TestClass();
        obj.TestMethod();
    }
    
    private void TestMethod()
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserialized()>
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim obj As New TestClass()
            obj.TestMethod()
        End Sub

        Sub TestMethod()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestOnDeserializationNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass : IDeserializationCallback
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender)
    {
        var obj = new TestClass();
        obj.TestMethod();
    }
    
    private void TestMethod()
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDeserializationCallback
        Private member As String
        
        Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim obj As New TestClass()
            obj.TestMethod()
        End Sub

        Sub TestMethod()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestOnDeserializingWithoutSerializableNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class TestClass
{
    private string member;

    [OnDeserializing()]
    internal void OnDeserializingMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    Class TestClass
        Private member As String
        
        <OnDeserializing()>
        Sub OnDeserializingMethod(ByVal context As StreamingContext)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestOnDeserializationWithoutSerializableNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class TestClass : IDeserializationCallback
{
    private string member;

    void IDeserializationCallback.OnDeserialization(Object sender)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    Class TestClass 
        Implements IDeserializationCallback
        Private member As String
        
        Sub OnDeserialization(ByVal sender As Object) Implements IDeserializationCallback.OnDeserialization
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestOnDeserializationWithoutIDeserializationCallbackNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    public void OnDeserialization(Object sender)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}");
        }

        [Fact]
        public async Task TestOnDeserializedWithEmptyMethodBodyNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        <OnDeserialized()>
        Sub OnDeserialized(ByVal context As StreamingContext)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestWithoutOnDeserializingAttributesNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    internal void OnDeserializingMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        Sub OnDeserializingMethod(ByVal context As StreamingContext)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestOnSerializedNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnSerialized()]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        Sub OnDeserializedMethod(ByVal context As StreamingContext)
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestFinalizeNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    ~TestClass()
    {
        var obj = new TestClass();
        obj.TestMethod();
    }
    
    private void TestMethod()
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        Sub Finalize()
            Dim obj As New TestClass()
            obj.TestMethod()
        End Sub

        Sub TestMethod()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestFinalizeWhenSubClassWithoutSerializableNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    ~TestClass()
    {
    }
}

public class SubTestClass : TestClass
{
    private string member;

    ~SubTestClass()
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Private member As String
        
        Protected Overrides Sub Finalize()
        End Sub
    End Class

    Class SubTestClass 
        Inherits TestClass
        Private member As String
        
        Protected Overrides Sub Finalize()
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestDisposeNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass : IDisposable
{
    private string member;
    bool disposed = false;

    public void Dispose()
    {
        var obj = new TestClass();
        obj.TestMethod();
        Dispose(true);
        GC.SuppressFinalize(this);           
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return; 
        }
      
        if (disposing) 
        {
            var obj = new TestClass();
            obj.TestMethod();
        }
      
        disposed = true;
    }

    private void TestMethod()
    {
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    <Serializable()> _
    Class TestClass
        Implements IDisposable
        Private member As String
        Protected disposed As Boolean = False
        
        Sub Dispose() Implements IDisposable.Dispose
            Dim obj As New TestClass()
            obj.TestMethod()
            Dispose(True)  
            GC.SuppressFinalize(Me) 
        End Sub

        Protected Overridable Sub Dispose(ByVal disposing As Boolean)
            If Not Me.disposed Then
                If disposing Then
                    Dim obj As New TestClass()
                    obj.TestMethod()
                End If
            End If
            Me.disposed = True
        End Sub

        Sub TestMethod()
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestDisposeWithoutSerializableNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class TestClass : IDisposable
{
    private string member;
    bool disposed = false;

    public void Dispose()
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            byte[] bytes = new byte[] {0x20, 0x20, 0x20};
            File.WriteAllBytes(""C:\\"", bytes);
        }

        disposed = true;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    Class TestClass
        Implements IDisposable
        Private member As String
        Protected disposed As Boolean = False
        
        Sub Dispose() Implements IDisposable.Dispose
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overridable Sub Dispose(ByVal disposing As Boolean)
            If Not Me.disposed Then
                If disposing Then
                    Dim bytes(9) As Byte
                    File.WriteAllBytes(""C:\\"", bytes)
                End If
            End If
            Me.disposed = True
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestDisposeNotImplementIDisposableNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public class TestClass
{
    private string member;
    bool disposed = false;

    public void Dispose()
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(""C:\\"", bytes);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            byte[] bytes = new byte[] {0x20, 0x20, 0x20};
            File.WriteAllBytes(""C:\\"", bytes);
        }

        disposed = true;
    }
}");

            await VerifyVB.VerifyAnalyzerAsync(@"
Imports System
Imports System.IO
Imports System.Runtime.Serialization

Namespace TestNamespace
    Class TestClass
        Private member As String
        Protected disposed As Boolean = False
        
        Sub Dispose()
            Dim bytes(9) As Byte
            File.WriteAllBytes(""C:\\"", bytes)
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overridable Sub Dispose(ByVal disposing As Boolean)
            If Not Me.disposed Then
                If disposing Then
                    Dim bytes(9) As Byte
                    File.WriteAllBytes(""C:\\"", bytes)
                End If
            End If
            Me.disposed = True
        End Sub
    End Class
End Namespace");
        }

        [Fact]
        public async Task TestUsingGenericwithTypeSpecifiedNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestGenericClass<T>
{
    private T memberInGeneric;

    public void TestGenericMethod()
    {
    }
}

[Serializable()]
public class TestClass : IDisposable
{
    private TestGenericClass<int> member;
    bool disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);           
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return; 
        }
      
        if (disposing) 
        {
        }
      
        disposed = true;
    }

    private void TestMethod()
    {
        member.TestGenericMethod();
    }
}");
        }

        [Fact]
        public async Task TestUsingInterfaceNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

interface TestInterface
{
    void TestInterfaceMethod();
}

[Serializable()]
public class TestInterfaceImplement : TestInterface
{
    public void TestInterfaceMethod()
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}

[Serializable()]
public class TestClass : IDisposable
{
    private TestInterface member;
    bool disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
        }

        disposed = true;
    }

    private void TestMethod()
    {
        member.TestInterfaceMethod();
    }
}");
        }

        [Fact]
        public async Task TestUsingAbstractClassNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

public abstract class TestAbstractClass
{
    public abstract void TestAbstractMethod();
}

[Serializable()]
public class TestDerivedClass : TestAbstractClass
{
    public override void TestAbstractMethod()
    {
        var path = ""C:\\"";
        var bytes = new byte[] {0x20, 0x20, 0x20};
        File.WriteAllBytes(path, bytes);
    }
}

[Serializable()]
public class TestClass : IDisposable
{
    private TestAbstractClass member;
    bool disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
        }

        disposed = true;
    }

    private void TestMethod()
    {
        member.TestAbstractMethod();
    }
}");
        }

        [Fact]
        public async Task TestLocalFunctionDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.IO;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserializing()]
    internal void OnDeserializingMethod(StreamingContext context)
    {
        byte[] bytes = new byte[] {0x20, 0x20, 0x20};
        ALocalFunction();

        void ALocalFunction()
        {
            File.WriteAllBytes(""C:\\"", bytes);
        }
    }
}",
            GetCSharpResultAt(
                12,
                19,
                "TestClass",
                "OnDeserializingMethod",
                "void File.WriteAllBytes(string path, byte[] bytes)"));
        }

        [Fact]
        public async Task TestLocalFunctionNoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
using System;
using System.Runtime.Serialization;

[Serializable()]
public class TestClass
{
    private string member;

    [OnDeserializing()]
    internal void OnDeserializingMethod(StreamingContext context)
    {
        ALocalFunction();

        void ALocalFunction()
        {
            object o = new Object();
        }
    }
}");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);

        private static DiagnosticResult GetBasicResultAt(int line, int column, params string[] arguments)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(arguments);
    }
}
