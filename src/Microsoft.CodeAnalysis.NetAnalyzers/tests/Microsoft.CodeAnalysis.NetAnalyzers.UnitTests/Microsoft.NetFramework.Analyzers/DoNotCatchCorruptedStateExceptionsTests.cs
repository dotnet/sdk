// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotCatchCorruptedStateExceptionsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetFramework.Analyzers.DoNotCatchCorruptedStateExceptionsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetFramework.Analyzers.UnitTests
{
    public class DoNotCatchCorruptedStateExceptionsTests
    {
        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithSecurityCriticalAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;

            namespace TestNamespace
            {
                class TestClass
                {
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security

            Namespace TestNamespace
                Class TestClass
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security

            Namespace TestNamespace
                Class TestClass
                    <SecurityCritical> _
                    Public Shared Function TestMethod() as Boolean
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                        Return True
                    End Function
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAttribute()
        {
            // Note this is a change from FxCop's previous behavior since we no longer consider SystemCritical.

            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(17, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(11, 25, "Public Shared Sub TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ",
           GetCA2153BasicResultAt(11, 25, "Public Shared Function TestMethod() As Double", "System.Exception")
           );
        }

        [Fact]
        public async Task CA2153TestCatchRethrowExceptionInMethodWithHpcseAndSecurityCriticalAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                            throw;
                        }
                    }
                }
            }");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                            Throw
                        End Try
                    End Sub
                End Class
            End Namespace
            ");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                            Throw
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAndSecurityCriticalAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(19, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Sub TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    < SecurityCritical > _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Function TestMethod() As Double", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    < SecurityCritical > _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As Exception
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(14, 25, "Public Shared Function TestMethod() As Double", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchInMethodWithHpcseAndSecurityCriticalAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(19, 25, "TestNamespace.TestClass.TestMethod()", "object")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Sub TestMethod()", "Object")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    < SecurityCritical > _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Function TestMethod() As Double", "Object")
            );
        }

        [Fact]
        public async Task CA2153TestCatchsystemExceptionInMethodWithHpcseAndSecurityCriticalAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (SystemException e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(19, 25, "TestNamespace.TestClass.TestMethod()", "System.SystemException")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e as System.SystemException
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Sub TestMethod()", "System.SystemException")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    < SecurityCritical > _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e as System.SystemException
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Function TestMethod() As Double", "System.SystemException")
            );
        }

        [Fact]
        public async Task CA2153TestCatchWithFilterInMethodWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch when (true)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(17, 25, "TestNamespace.TestClass.TestMethod()", "System.SystemException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch When True
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(11, 25, "Public Shared Sub TestMethod()", "System.SystemException"));
        }

        [Fact]
        public async Task CA2153TestCatchVariableWithFilterInMethodWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e) when (true)
                        {
                        }
                    }
                }
            }");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception When True
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAndSecurityCriticalClassScopeEverythingAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                [SecurityCritical(SecurityCriticalScope.Everything)]
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(19, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                <SecurityCritical(SecurityCriticalScope.Everything)> _
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(13, 25, "Public Shared Sub TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAndSecurityCriticalClassAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                [SecurityCritical]
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(19, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAndSecurityCriticalClassScopeExcplicitAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                [SecurityCritical(SecurityCriticalScope.Explicit)]
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(19, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAndSecurityCriticalL1Attributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            [assembly:SecurityCritical(SecurityCriticalScope.Everything)]
            [assembly:SecurityRules(SecurityRuleSet.Level1)]
            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInMethodWithHpcseAndSecurityCriticalL2Attributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            [assembly:SecurityCritical(SecurityCriticalScope.Everything)]
            [assembly:SecurityRules(SecurityRuleSet.Level2)]
            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInNestedClassMethodWithOuterHpcseAndSecurityCriticalScopeEverythingAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                [SecurityCritical(SecurityCriticalScope.Everything)]
                class TestClass
                {
                    class NestedClass
                    {
                        [HandleProcessCorruptedStateExceptions] 
                        public static void TestMethod()
                        {
                            try 
                            {
                                FileStream fileStream = new FileStream(""name"", FileMode.Create);
                            }
                            catch (Exception e)
                            {
                            }
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(21, 29, "TestNamespace.TestClass.NestedClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInNestedClassMethodWithInnerHpcseAndSecurityCriticalScopeEverythingAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [SecurityCritical(SecurityCriticalScope.Everything)]
                    class NestedClass
                    {
                        [HandleProcessCorruptedStateExceptions] 
                        public static void TestMethod()
                        {
                            try 
                            {
                                FileStream fileStream = new FileStream(""name"", FileMode.Create);
                            }
                            catch (Exception e)
                            {
                            }
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(21, 29, "TestNamespace.TestClass.NestedClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInNestedClassMethodwithInnerHpcseAndOuterSecurityCriticalAttributes()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [SecurityCritical(SecurityCriticalScope.Everything)]
                    class NestedClass
                    {
                        [HandleProcessCorruptedStateExceptions] 
                        public static void TestMethod()
                        {
                            try 
                            {
                                FileStream fileStream = new FileStream(""name"", FileMode.Create);
                            }
                            catch (Exception e)
                            {
                            }
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(21, 29, "TestNamespace.TestClass.NestedClass.TestMethod()", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInGetAccessorWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {      
                    public string SaveNewFile3
                    {
                        [HandleProcessCorruptedStateExceptions]
                        get
                        {
                            try
                            {
                                AccessViolation();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(""CATCH"");
                            }
                            return ""asdf"";
                        }
                    }

                    private static void AccessViolation()
                    {
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 29, "TestNamespace.TestClass.SaveNewFile3.get", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    private xi As Integer
                    Public ReadOnly Property X() As Integer
                        <HandleProcessCorruptedStateExceptions> _
                        <SecurityCritical> _
                        Get
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            Catch e As System.Exception
                            End Try
                            Return x
                        End Get
                    End Property
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(15, 29, "Public Property Get X() As Integer", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchInGetAccessorWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {      
                    public string SaveNewFile3
                    {
                        [HandleProcessCorruptedStateExceptions]
                        get
                        {
                            try
                            {
                                AccessViolation();
                            }
                            catch 
                            {
                                Console.WriteLine(""CATCH"");
                            }
                            return ""asdf"";
                        }
                    }

                    private static void AccessViolation()
                    {
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 29, "TestNamespace.TestClass.SaveNewFile3.get", "object")
            );
        }

        [Fact]
        public async Task CA2153TestCatchSystemExceptionInGetAccessorWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {      
                    public string SaveNewFile3
                    {
                        [HandleProcessCorruptedStateExceptions]
                        get
                        {
                            try
                            {
                                AccessViolation();
                            }
                            catch (SystemException ex)
                            {
                                Console.WriteLine(""CATCH"");
                            }
                            return ""asdf"";
                        }
                    }

                    private static void AccessViolation()
                    {
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 29, "TestNamespace.TestClass.SaveNewFile3.get", "System.SystemException")
            );
        }

        [Fact]
        public async Task CA2153TestCatchInSetAccessorWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {      
                    public string SaveNewFile3
                    {
                        [HandleProcessCorruptedStateExceptions]
                        set
                        {
                            try
                            {
                                AccessViolation();
                            }
                            catch 
                            {
                                Console.WriteLine(""CATCH"");
                            }
                        }
                    }

                    private static void AccessViolation()
                    {
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 29, "TestNamespace.TestClass.SaveNewFile3.set", "object")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInSetAccessorWithHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {      
                    private string file;
                    public string SaveNewFile3
                    {
                        [HandleProcessCorruptedStateExceptions]
                        set
                        {
                            try
                            {
                                AccessViolation();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(""CATCH"");
                            }
                            file = value;
                        }
                    }

                    private static void AccessViolation()
                    {
                    }
                }
            }",
            GetCA2153CSharpResultAt(21, 29, "TestNamespace.TestClass.SaveNewFile3.set", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    private xi As Integer
                    Public WriteOnly Property X() As Integer
                        <HandleProcessCorruptedStateExceptions> _
                        <SecurityCritical> _
                        Set
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            Catch e As System.Exception
                            End Try
                        End Set
                    End Property
                End Class
            End Namespace
            ",
           GetCA2153BasicResultAt(16, 29, "Public Property Set X(Value As Integer)", "System.Exception")
           );
        }

        [Fact]
        public async Task CA2153TestCatchIOExceptionInMethodHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions]
                    public static void TestMethod()
                    { 
                        try
                        {
                            FileStream fs = new FileStream(""fileName"", FileMode.Create);
                        }
                        catch (IOException ex)
                        {
                            throw ex;
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                        }
                    }
                }
            }"
            );
        }

        [Fact]
        public async Task CA2153TestCatchIOExceptionSwallowOtherExceptionInMethodHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions]
                    public static void TestMethod()
                    { 
                        try
                        {
                            FileStream fs = new FileStream(""fileName"", FileMode.Create);
                        }
                        catch (IOException ex)
                        {
                            throw ex;
                        }
                        catch
                        {
                        }
                        finally
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(22, 25, "TestNamespace.TestClass.TestMethod()", "object")
            );
        }

        [Fact]
        public async Task CA2153TestSwallowAccessViolationExceptionInMethodHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {   
                    [HandleProcessCorruptedStateExceptions]
                    public static void SaveNewFile7(string fileName)
                    {
                        try
                        {
                            unsafe
                            {
                                byte b = *(byte*)(8762765876); // some code that causes access violation
                            }
                        }
                        catch (AccessViolationException ex)
                        {
                            // the AV is ignored here
                        }
                        finally
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task CA2153TestSwallowAccessViolationExceptionThenSwallowOtherExceptionInMethodHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {   
                    [HandleProcessCorruptedStateExceptions]
                    public static void SaveNewFile7(string fileName)
                    {
                        try
                        {
                            unsafe
                            {
                                byte b = *(byte*)(8762765876); // some code that causes access violation
                            }
                        }
                        catch (AccessViolationException ex)
                        {
                            // the AV is ignored here
                        }
                        catch
                        {
                        }
                        finally
                        {
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(25, 25, "TestNamespace.TestClass.SaveNewFile7(string)", "object")
            );
        }

        [Fact]
        public async Task CA2153TestCatchExceptionThrowNotImplementedExceptionInMethodHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                            throw new NotImplementedException();
                        }
                    }
                }
            }");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                            Throw New NotImplementedException()
                        End Try
                    End Sub
                End Class
            End Namespace
            ");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Function TestMethod() As Double
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                            Throw New NotImplementedException()
                        End Try
                    Return 0
                    End Function
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CA2153TestCatchExceptionInnerCatchThrowIOExceptionInMethodHpcseAttribute()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [HandleProcessCorruptedStateExceptions] 
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        FileStream fileStream = null;
                        try
                        {
                            fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception)
                        {
                            try
                            {
                                FileStream  anotherFileStream = new FileStream(""newName"", FileMode.Create);
                            }
                            catch (IOException)
                            {
                                throw;
                            }
                        }
                    }
                }
            }",
            GetCA2153CSharpResultAt(20, 25, "TestNamespace.TestClass.TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Dim fileStream As FileStream = Nothing
                        Try
                            fileStream = New FileStream(""name"", FileMode.Create)
                        Catch outerException As System.Exception
                            Try
                                Dim anotherFileStream = New FileStream(""newName"", FileMode.Create)
                            Catch innerException As IOException
                                Throw
                            End Try
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(14, 25, "Public Shared Sub TestMethod()", "System.Exception")
            );

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <HandleProcessCorruptedStateExceptions> _
                    <SecurityCritical> _
                    Public Shared Function TestMethod() As Double
                        Dim fileStream As FileStream = Nothing
                        Try
                            fileStream = New FileStream(""name"", FileMode.Create)
                        Catch outerException As System.Exception
                            Try
                                Dim anotherFileStream = New FileStream(""newName"", FileMode.Create)
                            Catch innerException As IOException
                                Throw
                            End Try
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ",
            GetCA2153BasicResultAt(14, 25, "Public Shared Function TestMethod() As Double", "System.Exception")
            );
        }

        [Fact]
        public async Task CA2153TestCatchGeneralException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Security;
            using System.Runtime.ExceptionServices;

            namespace TestNamespace
            {
                class TestClass
                {
                    [SecurityCritical]
                    public static void TestMethod()
                    {
                        FileStream fileStream = null;
                        try
                        {
                            fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <SecurityCritical> _
                    Public Shared Sub TestMethod()
                        Dim fileStream As FileStream = Nothing
                        Try
                            fileStream = New FileStream(""name"", FileMode.Create)
                        Catch outerException As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Imports System.Security
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
                Class TestClass
                    <SecurityCritical> _
                    Public Shared Function TestMethod() As Double
                        Dim fileStream As FileStream = Nothing
                        Try
                            fileStream = New FileStream(""name"", FileMode.Create)
                        Catch outerException As System.Exception
                        End Try
                        Return 0
                    End Function
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CA2153TestCatchInsideLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
            using System;
            using System.IO;
            using System.Runtime.ExceptionServices;

            class TestClass
            {
                [HandleProcessCorruptedStateExceptions]
                public static void TestMethod()
                {
                    Action action = () =>
                    {
                        try
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                        }
                    };
                }
            }");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
	            Class TestClass
		            <HandleProcessCorruptedStateExceptions> _
		            Public Shared Sub TestMethod()
			            Dim action As Action = Function() 
				            Try
					            Dim fileStream As New FileStream(""name"", FileMode.Create)
				            Catch e As Exception
				            End Try
			            End Function
		            End Sub
	            End Class
            End Namespace
            ");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO
            Imports System.Runtime.ExceptionServices

            Namespace TestNamespace
	            Class TestClass
		            <HandleProcessCorruptedStateExceptions> _
		            Public Shared Function TestMethod() As Double
			            Dim action As Action = Function() 
				            Try
					            Dim fileStream As New FileStream(""name"", FileMode.Create)
				            Catch e As Exception
				            End Try
                            Return 0
			            End Function
		            End Function
	            End Class
            End Namespace
            ");
        }

        private static DiagnosticResult GetCA2153CSharpResultAt(int line, int column, string signature, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyCS.Diagnostic().WithLocation(line, column).WithArguments(signature, typeName);
#pragma warning restore RS0030 // Do not used banned APIs

        private static DiagnosticResult GetCA2153BasicResultAt(int line, int column, string signature, string typeName)
#pragma warning disable RS0030 // Do not used banned APIs
            => VerifyVB.Diagnostic().WithLocation(line, column).WithArguments(signature, typeName);
#pragma warning restore RS0030 // Do not used banned APIs
    }
}
