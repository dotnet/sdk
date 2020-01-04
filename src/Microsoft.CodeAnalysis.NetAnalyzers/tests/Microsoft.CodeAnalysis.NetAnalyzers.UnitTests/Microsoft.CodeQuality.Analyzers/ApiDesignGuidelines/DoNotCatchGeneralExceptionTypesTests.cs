// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Test.Utilities;
using Xunit;

namespace Microsoft.ApiDesignGuidelines.Analyzers.UnitTests
{
    public class DoNotCatchGeneralExceptionTypesTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotCatchGeneralExceptionTypesAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotCatchGeneralExceptionTypesAnalyzer();
        }

        [Fact]
        public void CSharp_Diagnostic_GeneralCatch()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (IOException e)
                        {
                        }
                        catch
                        {
                        }
                    }
                }
            }",
            GetCA1031CSharpResultAt(18, 25, "TestMethod"));
        }

        [Fact]
        public void Basic_Diagnostic_GeneralCatch()
        {
            VerifyBasic(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As IOException
                        Catch
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(10, 25, "TestMethod"));
        }

        [Fact]
        public void CSharp_Diagnostic_GeneralCatchInGetAccessor()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public int TestProperty
                    {
                        get
                        {
                            try
                            {
                                FileStream fileStream = new FileStream(""name"", FileMode.Create);
                            }
                            catch (IOException e)
                            {
                            }
                            catch
                            {
                            }
                            return 0;
                        }
                    }
                }
            }",
            GetCA1031CSharpResultAt(20, 29, "get_TestProperty"));
        }

        [Fact]
        public void Basic_Diagnostic_GeneralCatchInGetAccessor()
        {
            VerifyBasic(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public ReadOnly Property X() As Integer
                        Get
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            Catch e As IOException
                            Catch
                            End Try
                            Return 0
                        End Get
                    End Property
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(11, 29, "get_X"));
        }

        [Fact]
        public void CSharp_NoDiagnostic_GeneralCatchRethrow()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (IOException e)
                        {
                        }
                        catch
                        {
                            throw;
                        }
                    }
                }
            }");
        }

        [Fact]
        public void Basic_NoDiagnostic_GeneralCatchRethrow()
        {
            VerifyBasic(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As IOException
                        Catch
                            Throw
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public void CSharp_NoDiagnostic_GeneralCatchThrowNew()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (IOException e)
                        {
                        }
                        catch
                        {
                            throw new NotImplementedException();
                        }
                    }
                }
            }");
        }

        [Fact]
        public void Basic_NoDiagnostic_GeneralCatchThrowNew()
        {
            VerifyBasic(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As IOException
                        Catch
                            Throw New System.NotImplementedException()
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public void CSharp_Diagnostic_GeneralCatchWithRethrowFromSpecificCatch()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (IOException e)
                        {
                            throw;
                        }
                        catch
                        {
                        }
                    }
                }
            }",
            GetCA1031CSharpResultAt(19, 25, "TestMethod"));
        }

        [Fact]
        public void Basic_Diagnostic_GeneralCatchWithRethrowFromSpecificCatch()
        {
            VerifyBasic(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As IOException
                            Throw
                        Catch
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(11, 25, "TestMethod"));
        }

        [Fact]
        public void CSharp_Diagnostic_GenericException()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
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
            GetCA1031CSharpResultAt(15, 25, "TestMethod"));
        }

        [Fact]
        public void Basic_Diagnostic_GenericException()
        {
            VerifyBasic(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(10, 25, "TestMethod"));
        }

        [Fact]
        public void CSharp_NoDiagnostic_GenericExceptionRethrown()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                            throw e;
                        }
                    }
                }
            }");
        }

        [Fact]
        public void Basic_NoDiagnostic_GenericExceptionRethrown()
        {
            VerifyBasic(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As Exception
                            Throw e
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public void CSharp_NoDiagnostic_ThrowNewWrapped()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception e)
                        {
                            throw new AggregateException(e);
                        }
                    }
                }
            }");
        }

        [Fact]
        public void Basic_NoDiagnostic_ThrowNewWrapped()
        {
            VerifyBasic(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As Exception
                            Throw New AggregateException(e)
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public void CSharp_Diagnostic_SystemException()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
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
            GetCA1031CSharpResultAt(15, 25, "TestMethod"));
        }

        [Fact]
        public void Basic_Diagnostic_SystemException()
        {
            VerifyBasic(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(9, 25, "TestMethod"));
        }

        [Fact]
        public void CSharp_Diagnostic_GeneralCatchWithFilter()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;
            namespace TestNamespace
            {
                class TestClass
                {
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
            GetCA1031CSharpResultAt(14, 25, "TestMethod"));
        }

        [Fact]
        public void Basic_Diagnostic_GeneralCatchWithFilter()
        {
            VerifyBasic(@"
            Imports System.IO
            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch When True
                        End Try
                    End Sub
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(8, 25, "TestMethod"));
        }

        [Fact]
        public void CSharp_Diagnostic_GenericExceptionWithoutVariableWithFilter()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;
            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        try 
                        {
                            FileStream fileStream = new FileStream(""name"", FileMode.Create);
                        }
                        catch (Exception) when (true)
                        {
                        }
                    }
                }
            }",
            GetCA1031CSharpResultAt(14, 25, "TestMethod"));
        }

        [Fact]
        public void CSharp_NoDiagnostic_GenericExceptionWithVariableWithFilter()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;
            namespace TestNamespace
            {
                class TestClass
                {
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
        }

        [Fact]
        public void Basic_NoDiagnostic_GenericExceptionWithVariableWithFilter()
        {
            VerifyBasic(@"
            Imports System
            Imports System.IO
            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As Exception When True
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public void CSharp_Diagnostic_GeneralCatchInLambdaExpression()
        {
            VerifyCSharp(@"
            using System;
            using System.IO;

            namespace TestNamespace
            {
                class TestClass
                {
                    public static void TestMethod()
                    {
                        Action action = () =>
                        {
                            try
                            {
                                FileStream fileStream = new FileStream(""name"", FileMode.Create);
                            }
                            catch
                            {
                            }
                        };
                    }
                }
            }",
            GetCA1031CSharpResultAt(17, 29, "TestMethod"));
        }

        [Fact]
        public void Basic_Diagnostic_GeneralCatchInLambdaExpression()
        {
            VerifyBasic(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Dim action As Action = Function() 
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            Catch
                            End Try
                        End Function
                    End Sub
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(11, 29, "TestMethod"));

            VerifyBasic(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Function TestMethod() As Double
                        Dim action As Action = Function() 
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            Catch
                            End Try
                            Return 0
                        End Function
                    End Function
                End Class
            End Namespace
            ",
            GetCA1031BasicResultAt(11, 29, "TestMethod"));
        }

        [Fact, WorkItem(2518, "https://github.com/dotnet/roslyn-analyzers/issues/2518")]
        public void CSharp_NoDiagnostic_SpecificExceptionWithoutVariable()
        {
            VerifyCSharp(@"
            using System;

            public class Class1
            {
                void M()
                {
                    try
                    {
                    }
                    catch (OperationCanceledException)
                    {
                        // Comment
                    }
                }
            }");
        }

        [Theory]
        [WorkItem(2713, "https://github.com/dotnet/roslyn-analyzers/issues/2713")]
        // No configuration - validate no diagnostics in default configuration
        [InlineData("")]
        // Match by type name
        [InlineData("dotnet_code_quality.disallowed_symbol_names = NullReferenceException")]
        // Setting only for Rule ID
        [InlineData("dotnet_code_quality." + DoNotCatchGeneralExceptionTypesAnalyzer.RuleId + ".disallowed_symbol_names = NullReferenceException")]
        // Match by type documentation ID
        [InlineData(@"dotnet_code_quality.disallowed_symbol_names = T:System.NullReferenceException")]
        public void EditorConfigConfiguration_DisallowedExceptionTypes(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length > 0)
            {
                expected = new DiagnosticResult[]
                {
                    GetCA1031CSharpResultAt(7, 9, "M1")
                };
            }

            VerifyCSharp(@"
class Test
{
    void M1(string param)
    {
        try { }
        catch (System.NullReferenceException ex) { }
    }
}", GetEditorConfigAdditionalFile(editorConfigText), expected);

            expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length > 0)
            {
                expected = new DiagnosticResult[]
                {
                    GetCA1031BasicResultAt(5, 9, "M1")

                };
            }

            VerifyBasic(@"
Class Test
    Private Sub M1(param As String)
        Try
        Catch ex As System.NullReferenceException
        End Try
    End Sub
End Class", GetEditorConfigAdditionalFile(editorConfigText), expected);
        }

        private static DiagnosticResult GetCA1031CSharpResultAt(int line, int column, string signature)
        {
            return GetCSharpResultAt(line, column, DoNotCatchGeneralExceptionTypesAnalyzer.Rule, signature);
        }

        private static DiagnosticResult GetCA1031BasicResultAt(int line, int column, string signature)
        {
            return GetBasicResultAt(line, column, DoNotCatchGeneralExceptionTypesAnalyzer.Rule, signature);
        }
    }
}