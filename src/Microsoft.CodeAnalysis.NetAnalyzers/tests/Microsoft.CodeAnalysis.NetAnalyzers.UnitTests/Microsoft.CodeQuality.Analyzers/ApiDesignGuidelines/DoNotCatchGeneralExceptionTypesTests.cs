// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotCatchGeneralExceptionTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.DoNotCatchGeneralExceptionTypesAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.ApiDesignGuidelines.Analyzers.UnitTests
{
    public class DoNotCatchGeneralExceptionTypesTests
    {
        [Fact]
        public async Task CSharp_Diagnostic_GeneralCatch()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                        [|catch|]
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_GeneralCatch()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As IOException
                        [|Catch|]
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CSharp_Diagnostic_GeneralCatchInGetAccessor()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                            [|catch|]
                            {
                            }
                            return 0;
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_GeneralCatchInGetAccessor()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public ReadOnly Property X() As Integer
                        Get
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            Catch e As IOException
                            [|Catch|]
                            End Try
                            Return 0
                        End Get
                    End Property
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CSharp_NoDiagnostic_GeneralCatchRethrow()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task Basic_NoDiagnostic_GeneralCatchRethrow()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_NoDiagnostic_GeneralCatchThrowNew()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task Basic_NoDiagnostic_GeneralCatchThrowNew()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_Diagnostic_GeneralCatchWithRethrowFromSpecificCatch()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                        [|catch|]
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_GeneralCatchWithRethrowFromSpecificCatch()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        Catch e As IOException
                            Throw
                        [|Catch|]
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CSharp_Diagnostic_GenericException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                        [|catch|] (Exception e)
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_GenericException()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        [|Catch|] e As Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CSharp_NoDiagnostic_GenericExceptionRethrown()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task Basic_NoDiagnostic_GenericExceptionRethrown()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_NoDiagnostic_ThrowNewWrapped()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task Basic_NoDiagnostic_ThrowNewWrapped()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_Diagnostic_SystemException()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                        [|catch|] (SystemException e)
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_SystemException()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        [|Catch|] e As System.Exception
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CSharp_Diagnostic_GeneralCatchWithFilter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                        [|catch|] when (true)
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_GeneralCatchWithFilter()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System.IO
            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Try
                            Dim fileStream As New FileStream(""name"", FileMode.Create)
                        [|Catch|] When True
                        End Try
                    End Sub
                End Class
            End Namespace
            ");
        }

        [Fact]
        public async Task CSharp_Diagnostic_GenericExceptionWithoutVariableWithFilter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                        [|catch|] (Exception) when (true)
                        {
                        }
                    }
                }
            }");
        }

        [Fact]
        public async Task CSharp_NoDiagnostic_GenericExceptionWithVariableWithFilter()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task Basic_NoDiagnostic_GenericExceptionWithVariableWithFilter()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
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
        public async Task CSharp_Diagnostic_GeneralCatchInLambdaExpression()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
                            [|catch|]
                            {
                            }
                        };
                    }
                }
            }");
        }

        [Fact]
        public async Task Basic_Diagnostic_GeneralCatchInLambdaExpression()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Sub TestMethod()
                        Dim action As Action = Function() 
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            [|Catch|]
                            End Try
                        End Function
                    End Sub
                End Class
            End Namespace
            ");

            await VerifyVB.VerifyAnalyzerAsync(@"
            Imports System
            Imports System.IO

            Namespace TestNamespace
                Class TestClass
                    Public Shared Function TestMethod() As Double
                        Dim action As Action = Function() 
                            Try
                                Dim fileStream As New FileStream(""name"", FileMode.Create)
                            [|Catch|]
                            End Try
                            Return 0
                        End Function
                    End Function
                End Class
            End Namespace
            ");
        }

        [Fact, WorkItem(2518, "https://github.com/dotnet/roslyn-analyzers/issues/2518")]
        public async Task CSharp_NoDiagnostic_SpecificExceptionWithoutVariable()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
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
        public async Task EditorConfigConfiguration_DisallowedExceptionTypes(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
class Test
{{
    void M1(string param)
    {{
        try {{ }}
        {(editorConfigText.Length > 0 ? "[|catch|]" : "catch")} (System.NullReferenceException ex) {{ }}
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Class Test
    Private Sub M1(param As String)
        Try
        {(editorConfigText.Length > 0 ? "[|Catch|]" : "Catch")} ex As System.NullReferenceException
        End Try
    End Sub
End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }

        [Theory, WorkItem(3211, "https://github.com/dotnet/roslyn-analyzers/issues/3211")]
        // No configuration - validate no diagnostics in default configuration
        [InlineData("")]
        // Check with exact method signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = M:SomeNamespace.Test.M1(System.String)")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M:SomeNamespace.Test.M1(System.String)")]
        // Check with wildcard method signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = M:SomeNamespace.Test.M*")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = M:SomeNamespace.Test.M*")]
        // Check with exact type signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = T:SomeNamespace.Test")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = T:SomeNamespace.Test")]
        // Check with wildcard type signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = T:SomeNamespace.Tes*")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = T:SomeNamespace.Tes*")]
        // Check with exact namespace signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = N:SomeNamespace")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = N:SomeNamespace")]
        // Check with wildcard namespace signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = N:Some*")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = N:Some*")]
        // Check with match all signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = M1")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = Test")]
        // Check with wildcard signature
        [InlineData("dotnet_code_quality.CA1031.excluded_symbol_names = M1*")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = Tes*")]
        public async Task CA1031_EditorConfig_ExcludedSymbolNames(string editorConfigText)
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        @"
namespace SomeNamespace
{
    class Test
    {
        void M1(string param)
        {
            try { }"
            + (editorConfigText.Length == 0 ? "[|catch|]" : "catch") + @" (System.Exception ex) { }
        }
    }
}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
            }.RunAsync();

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
Namespace SomeNamespace
    Class Test
        Private Sub M1(param As String)
            Try
            {(editorConfigText.Length == 0 ? "[|Catch|]" : "Catch")} ex As System.Exception
            End Try
        End Sub
    End Class
End Namespace"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                }
            }.RunAsync();
        }
    }
}