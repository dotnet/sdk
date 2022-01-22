// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicSecurityCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotUseInsecureDtdProcessingAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public partial class DoNotUseInsecureDtdProcessingAnalyzerTests
    {
        private static DiagnosticResult GetCA3075DataViewCSharpResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleReviewDtdProcessingProperties).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA3075DataViewBasicResultAt(int line, int column)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic(DoNotUseInsecureDtdProcessingAnalyzer.RuleReviewDtdProcessingProperties).WithLocation(line, column);
#pragma warning restore RS0030 // Do not used banned APIs

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerSetCollectionStringShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

namespace TestNamespace
{
    public class ReviewDataViewConnectionString
    {
        public void TestMethod(string src)
        {
            DataSet ds = new DataSet();
            ds.DefaultViewManager.DataViewSettingCollectionString = src;
        }
    }
}
",
                GetCA3075DataViewCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Namespace TestNamespace
    Public Class ReviewDataViewConnectionString
        Public Sub TestMethod(src As String)
            Dim ds As New DataSet()
            ds.DefaultViewManager.DataViewSettingCollectionString = src
        End Sub
    End Class
End Namespace",
                GetCA3075DataViewBasicResultAt(8, 13)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagernInGetShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
    public DataSet Test
    {
        get {
            var src = """";
            DataSet ds = new DataSet();
            ds.DefaultViewManager.DataViewSettingCollectionString = src;
            return ds;
        }
    }
}",
                GetCA3075DataViewCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Public ReadOnly Property Test() As DataSet
        Get
            Dim src = """"
            Dim ds As New DataSet()
            ds.DefaultViewManager.DataViewSettingCollectionString = src
            Return ds
        End Get
    End Property
End Class",
                GetCA3075DataViewBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerInSetShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
DataSet privateDoc;
public DataSet GetDoc
        {
            set
            {
                if (value == null)
                {
                    var src = """";
                    DataSet ds = new DataSet();
                    ds.DefaultViewManager.DataViewSettingCollectionString = src;
                    privateDoc = ds;
                }
                else
                    privateDoc = value;
            }
        }
}",
                GetCA3075DataViewCSharpResultAt(15, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Private privateDoc As DataSet
    Public WriteOnly Property GetDoc() As DataSet
        Set
            If value Is Nothing Then
                Dim src = """"
                Dim ds As New DataSet()
                ds.DefaultViewManager.DataViewSettingCollectionString = src
                privateDoc = ds
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075DataViewBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerInTryBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
  using System;
  using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try
            {
                var src = """";
                DataSet ds = new DataSet();
                ds.DefaultViewManager.DataViewSettingCollectionString = src;
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075DataViewCSharpResultAt(13, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
            Dim src = """"
            Dim ds As New DataSet()
            ds.DefaultViewManager.DataViewSettingCollectionString = src
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerInCatchBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
   using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception)
            {
                var src = """";
                DataSet ds = new DataSet();
                ds.DefaultViewManager.DataViewSettingCollectionString = src;
            }
            finally { }
        }
    }",
                GetCA3075DataViewCSharpResultAt(14, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim src = """"
            Dim ds As New DataSet()
            ds.DefaultViewManager.DataViewSettingCollectionString = src
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
   using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception) { throw; }
            finally
            {
                var src = """";
                DataSet ds = new DataSet();
                ds.DefaultViewManager.DataViewSettingCollectionString = src;
            }
        }
    }",
                GetCA3075DataViewCSharpResultAt(15, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim src = """"
            Dim ds As New DataSet()
            ds.DefaultViewManager.DataViewSettingCollectionString = src
        End Try
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerInAsyncAwaitShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
 using System.Threading.Tasks;
using System.Data;

    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => {
                var src = """";
                DataSet ds = new DataSet();
                ds.DefaultViewManager.DataViewSettingCollectionString = src;
            });
        }

        private async void TestMethod2()
        {
            await TestMethod();
        }
    }",
                GetCA3075DataViewCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Threading.Tasks
Imports System.Data

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim src = """"
        Dim ds As New DataSet()
        ds.DefaultViewManager.DataViewSettingCollectionString = src

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(10, 9)
            );
        }

        [Fact]
        public async Task UseDataSetDefaultDataViewManagerInDelegateShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
    delegate void Del();

    Del d = delegate () {
        var src = """";
        DataSet ds = new DataSet();
        ds.DefaultViewManager.DataViewSettingCollectionString = src;
    };
}",
                GetCA3075DataViewCSharpResultAt(11, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim src = """"
    Dim ds As New DataSet()
    ds.DefaultViewManager.DataViewSettingCollectionString = src

End Sub
End Class",
                GetCA3075DataViewBasicResultAt(10, 5)
            );
        }

        [Fact]
        public async Task UseDataViewManagerSetCollectionStringShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

namespace TestNamespace
{
    public class ReviewDataViewConnectionString
    {
        public void TestMethod(string src)
        {
            DataViewManager manager = new DataViewManager();
            manager.DataViewSettingCollectionString = src;
        }
    }
}
",
                GetCA3075DataViewCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Namespace TestNamespace
    Public Class ReviewDataViewConnectionString
        Public Sub TestMethod(src As String)
            Dim manager As New DataViewManager()
            manager.DataViewSettingCollectionString = src
        End Sub
    End Class
End Namespace",
                GetCA3075DataViewBasicResultAt(8, 13)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInGetShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
    public DataViewManager Test
    {
        get {
            var src = """";
            DataViewManager manager = new DataViewManager();
            manager.DataViewSettingCollectionString = src;
            return manager;
        }
    }
}",
                GetCA3075DataViewCSharpResultAt(11, 13)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Public ReadOnly Property Test() As DataViewManager
        Get
            Dim src = """"
            Dim manager As New DataViewManager()
            manager.DataViewSettingCollectionString = src
            Return manager
        End Get
    End Property
End Class",
                GetCA3075DataViewBasicResultAt(9, 13)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInSetShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
DataViewManager privateDoc;
public DataViewManager GetDoc
        {
            set
            {
                if (value == null)
                {
                    var src = """";
                    DataViewManager manager = new DataViewManager();
                    manager.DataViewSettingCollectionString = src;
                    privateDoc = manager;
                }
                else
                    privateDoc = value;
            }
        }
}",
                GetCA3075DataViewCSharpResultAt(15, 21)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Private privateDoc As DataViewManager
    Public WriteOnly Property GetDoc() As DataViewManager
        Set
            If value Is Nothing Then
                Dim src = """"
                Dim manager As New DataViewManager()
                manager.DataViewSettingCollectionString = src
                privateDoc = manager
            Else
                privateDoc = value
            End If
        End Set
    End Property
End Class",
                GetCA3075DataViewBasicResultAt(11, 17)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInTryBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
  using System;
  using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try
            {
                var src = """";
                DataViewManager manager = new DataViewManager();
                manager.DataViewSettingCollectionString = src;
            }
            catch (Exception) { throw; }
            finally { }
        }
    }",
                GetCA3075DataViewCSharpResultAt(13, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
            Dim src = """"
            Dim manager As New DataViewManager()
            manager.DataViewSettingCollectionString = src
        Catch generatedExceptionName As Exception
            Throw
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(10, 13)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInCatchBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
   using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception)
            {
                var src = """";
                DataViewManager manager = new DataViewManager();
                manager.DataViewSettingCollectionString = src;
            }
            finally { }
        }
    }",
                GetCA3075DataViewCSharpResultAt(14, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Dim src = """"
            Dim manager As New DataViewManager()
            manager.DataViewSettingCollectionString = src
        Finally
        End Try
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(11, 13)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInFinallyBlockShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
   using System;
   using System.Data;

    class TestClass
    {
        private void TestMethod()
        {
            try { }
            catch (Exception) { throw; }
            finally
            {
                var src = """";
                DataViewManager manager = new DataViewManager();
                manager.DataViewSettingCollectionString = src;
            }
        }
    }",
                GetCA3075DataViewCSharpResultAt(15, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System
Imports System.Data

Class TestClass
    Private Sub TestMethod()
        Try
        Catch generatedExceptionName As Exception
            Throw
        Finally
            Dim src = """"
            Dim manager As New DataViewManager()
            manager.DataViewSettingCollectionString = src
        End Try
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(13, 13)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInAsyncAwaitShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
 using System.Threading.Tasks;
using System.Data;

    class TestClass
    {
        private async Task TestMethod()
        {
            await Task.Run(() => {
                var src = """";
                DataViewManager manager = new DataViewManager();
                manager.DataViewSettingCollectionString = src;
            });
        }

        private async void TestMethod2()
        {
            await TestMethod();
        }
    }",
                GetCA3075DataViewCSharpResultAt(12, 17)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Threading.Tasks
Imports System.Data

Class TestClass
    Private Async Function TestMethod() As Task
        Await Task.Run(Function() 
        Dim src = """"
        Dim manager As New DataViewManager()
        manager.DataViewSettingCollectionString = src

End Function)
    End Function

    Private Async Sub TestMethod2()
        Await TestMethod()
    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(10, 9)
            );
        }

        [Fact]
        public async Task UseDataViewManagerInDelegateShouldGenerateDiagnosticAsync()
        {
            await VerifyCSharpAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
using System.Data;

class TestClass
{
    delegate void Del();

    Del d = delegate () {
        var src = """";
        DataViewManager manager = new DataViewManager();
        manager.DataViewSettingCollectionString = src;
    };
}",
                GetCA3075DataViewCSharpResultAt(11, 9)
            );

            await VerifyVisualBasicAnalyzerAsync(
                ReferenceAssemblies.NetFramework.Net472.Default,
                @"
Imports System.Data

Class TestClass
    Private Delegate Sub Del()

    Private d As Del = Sub() 
    Dim src = """"
    Dim manager As New DataViewManager()
    manager.DataViewSettingCollectionString = src

    End Sub
End Class",
                GetCA3075DataViewBasicResultAt(10, 5)
            );
        }
    }
}
