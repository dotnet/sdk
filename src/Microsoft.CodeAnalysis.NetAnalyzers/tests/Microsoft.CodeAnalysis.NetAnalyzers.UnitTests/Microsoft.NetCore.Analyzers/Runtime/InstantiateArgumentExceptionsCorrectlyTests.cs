// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.InstantiateArgumentExceptionsCorrectlyAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.InstantiateArgumentExceptionsCorrectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.InstantiateArgumentExceptionsCorrectlyAnalyzer,
    Microsoft.NetCore.Analyzers.Runtime.InstantiateArgumentExceptionsCorrectlyFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class InstantiateArgumentExceptionsCorrectlyTests
    {
        [Fact]
        public async Task ArgumentException_NoArguments_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException();
                    }
                }",
                GetCSharpNoArgumentsExpectedResult(6, 31, "ArgumentException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException()
                    End Sub
                End Class",
                GetBasicNoArgumentsExpectedResult(4, 31, "ArgumentException"));
        }

        [Fact]
        public async Task ArgumentException_EmptyParameterNameArgument_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException("""");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException("""")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentNullException_SpaceParameterArgument_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException("" "");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", " ", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException("" "")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", " ", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentNullException_NameofNonParameter_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        var v = new object();
                        throw new System.ArgumentNullException(nameof(v));
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(7, 31, "Test", "v", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Dim v As New Object()
                        Throw New System.ArgumentNullException(NameOf(v))
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(5, 31, "Test", "v", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentException_ParameterNameAsMessage_WarnsAndCodeFixesWithNameOfAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(null, nameof(first));
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentException(""first"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "first", "message", "ArgumentException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentException(Nothing, NameOf(first))
    End Sub
End Class");
        }

        [Fact]
        public async Task ArgumentException_ReversedArguments_WarnsAndCodeFixesWithNameOfAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"", ""first is incorrect"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"", nameof(first));
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentException(""first"", ""first is incorrect"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "first", "message", "ArgumentException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentException(""first is incorrect"", NameOf(first))
    End Sub
End Class");
        }

        [Fact]
        public async Task ArgumentException_ParameterWithNameofAsMessage_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(nameof(first));
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException")
                , @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(null, nameof(first));
                    }
                }");
        }

        [Fact]
        public async Task ArgumentException_ReversedArgumentsWithNameof_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(nameof(first), ""first is incorrect"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"", nameof(first));
                    }
                }");
        }

        [Fact]
        public async Task ArgumentException_Reversed3Arguments_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"", ""first is incorrect"", null);
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"", nameof(first), null);
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentException(""first"", ""first is incorrect"", Nothing)
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "first", "message", "ArgumentException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentException(""first is incorrect"", NameOf(first), Nothing)
    End Sub
End Class");
        }

        [Fact]
        public async Task ArgumentNullException_NoArguments_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException();
                    }
                }",
                GetCSharpNoArgumentsExpectedResult(6, 31, "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException()
                    End Sub
                End Class",
                GetBasicNoArgumentsExpectedResult(4, 31, "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentNullException_MessageAsParameterName_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first is null"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is null", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentNullException_ReversedArguments_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first is null"", ""first"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentNullException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(nameof(first), ""first is null"");
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentNullException(""first is null"", ""first"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "first", "message", "ArgumentNullException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentNullException(NameOf(first), ""first is null"")
    End Sub
End Class");
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_NoArguments_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException();
                    }
                }",
                GetCSharpNoArgumentsExpectedResult(6, 31, "ArgumentOutOfRangeException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException()
                    End Sub
                End Class",
                GetBasicNoArgumentsExpectedResult(4, 31, "ArgumentOutOfRangeException"));
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_MessageAsParameterName_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first is out of range"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException(""first is out of range"")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"));
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_ReversedArguments_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first is out of range"", ""first"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentOutOfRangeException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(nameof(first), ""first is out of range"");
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentOutOfRangeException(""first is out of range"", ""first"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "first", "message", "ArgumentOutOfRangeException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.ArgumentOutOfRangeException(NameOf(first), ""first is out of range"")
    End Sub
End Class");
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_Reversed3Arguments_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        var val = new object();
                        throw new System.ArgumentOutOfRangeException(""first is out of range"", val, ""first"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(7, 31, "Test", "first", "message", "ArgumentOutOfRangeException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        var val = new object();
                        throw new System.ArgumentOutOfRangeException(nameof(first), val, ""first is out of range"");
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Dim val = New Object()
        Throw New System.ArgumentOutOfRangeException(""first is out of range"", val, ""first"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(5, 15, "Test", "first", "message", "ArgumentOutOfRangeException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Dim val = New Object()
        Throw New System.ArgumentOutOfRangeException(NameOf(first), val, ""first is out of range"")
    End Sub
End Class");
        }

        [Fact]
        public async Task DuplicateWaitObjectException_NoArguments_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException();
                    }
                }",
                GetCSharpNoArgumentsExpectedResult(6, 31, "DuplicateWaitObjectException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException()
                    End Sub
                End Class",
                GetBasicNoArgumentsExpectedResult(4, 31, "DuplicateWaitObjectException"));
        }

        [Fact]
        public async Task DuplicateWaitObjectException_MessageAsParameterName_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first is duplicate"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException(""first is duplicate"")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"));
        }

        [Fact]
        public async Task DuplicateWaitObjectException_ReversedArguments_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first is duplicate"", ""first"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "DuplicateWaitObjectException"), @"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(nameof(first), ""first is duplicate"");
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.DuplicateWaitObjectException(""first is duplicate"", ""first"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "first", "message", "DuplicateWaitObjectException"), @"
Public Class [MyClass]
    Public Sub Test(first As String)
        Throw New System.DuplicateWaitObjectException(NameOf(first), ""first is duplicate"")
    End Sub
End Class");
        }

        [Fact]
        public async Task ArgumentNullException_ParentHasNoParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test()
                    {
                        throw new System.ArgumentNullException(""Invalid argument"");
                    }
                }");
        }

        [Fact]
        public async Task ArgumentException_ParentHasNoParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test()
                    {
                        throw new System.ArgumentException(""Invalid argument"", ""test"");
                    }
                }");
        }

        [Fact]
        public async Task ArgumentException_VariableUsed_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string paramName, string message)
                    {
                        throw new System.ArgumentException(paramName, message);
                    }
                }");
        }

        [Fact]
        public async Task ArgumentException_NoArguments_ParentMethod_HasNoParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test()
                    {
                        throw new System.ArgumentException();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test()
                       Throw New System.ArgumentException()
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentException_CorrectMessage_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentException(""first is incorrect"")
                   End Sub
               End Class");
        }

        [Fact, WorkItem(6863, "https://github.com/dotnet/roslyn-analyzers/issues/6863")]
        public async Task ArgumentException_MessageStartWithParameterNameFollowedByPunctuation_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test1(string first)
                    {
                        throw new System.ArgumentException(""first.Length is incorrect"", nameof(first));
                    }

                    public void Test2(string first)
                    {
                        throw new System.ArgumentException(""first[0] is incorrect"", nameof(first));
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test1(first As String)
                       Throw New System.ArgumentException(""first.Length is incorrect"", NameOf(first))
                   End Sub

                   Public Sub Test2(first As String)
                       Throw New System.ArgumentException(""first(0) is incorrect"", NameOf(first))
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentException_GenericParameterName_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test<TEnum>(TEnum first)
                    {
                        throw new System.ArgumentException(""first is incorrect"", nameof(TEnum));
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(Of TEnum)(ByVal first As TEnum)
                        Throw New System.ArgumentException(""first is incorrect"", NameOf(TEnum))
                    End Sub
                End Class");
        }

        [Fact]
        public async Task ArgumentException_GenericParameterName_WrongPosition_WarnsAndCodeFixesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"
                public class Class
                {
                    public void Test<TEnum>(TEnum first)
                    {
                        throw new System.ArgumentException(""TEnum"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "TEnum", "message", "ArgumentException"), @"
                public class Class
                {
                    public void Test<TEnum>(TEnum first)
                    {
                        throw new System.ArgumentException(null, nameof(TEnum));
                    }
                }");

            await VerifyVB.VerifyCodeFixAsync(@"
Public Class [MyClass]
    Public Sub Test(Of TEnum)(ByVal first As TEnum)
        Throw New System.ArgumentException(""TEnum"")
    End Sub
End Class",
                GetBasicIncorrectMessageExpectedResult(4, 15, "Test", "TEnum", "message", "ArgumentException"), @"
Public Class [MyClass]
    Public Sub Test(Of TEnum)(ByVal first As TEnum)
        Throw New System.ArgumentException(Nothing, NameOf(TEnum))
    End Sub
End Class");
        }

        [Theory]
        [InlineData("public", "dotnet_code_quality.api_surface = private", false)]
        [InlineData("private", "dotnet_code_quality.api_surface = internal, public", false)]
        [InlineData("public", "dotnet_code_quality.CA2208.api_surface = private, public", true)]
        [InlineData("public", "dotnet_code_quality.CA2208.api_surface = internal, private", false)]
        [InlineData("public", "dotnet_code_quality.CA2208.api_surface = Friend, Private", false)]
        [InlineData("public", @"dotnet_code_quality.api_surface = all
                                        dotnet_code_quality.CA2208.api_surface = private", false)]
        [InlineData("public", "dotnet_code_quality.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.api_surface = internal, public", true)]
        [InlineData("public", "dotnet_code_quality.CA2208.api_surface = public", true)]
        [InlineData("public", "dotnet_code_quality.CA2208.api_surface = all", true)]
        [InlineData("public", "dotnet_code_quality.CA2208.api_surface = public, private", true)]
        [InlineData("public", @"dotnet_code_quality.api_surface = internal
                                        dotnet_code_quality.CA2208.api_surface = public", true)]
        [InlineData("public", "", true)]
        [InlineData("protected", "", true)]
        [InlineData("private", "", true)]
        [InlineData("protected", "dotnet_code_quality.CA2208.api_surface = public", true)]
        public async Task EditorConfigConfiguration_ApiSurfaceOption_TestAsync(string accessibility, string editorConfigText, bool expectDiagnostic)
        {
            var exception = expectDiagnostic ? @"[|new System.ArgumentNullException(""first is null"")|]" : @"new System.ArgumentNullException(""first is null"")";

            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
public class C
{{
    {accessibility} void Test(string first)
    {{
         throw {exception};
    }}
}}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();

            exception = expectDiagnostic ? @"[|New System.ArgumentNullException(""first is null"")|]" : @"New System.ArgumentNullException(""first is null"")";

            await new VerifyVB.Test
            {
                TestState =
                {
                    Sources =
                    {
                    $@"
 Public Class C
    {accessibility} Sub Test(first As String)
        Throw {exception}
     End Sub
 End Class"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
") }
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();
        }

        [Fact]
        public async Task EditorConfigConfiguredPublic_PrivateMethods_TriggeringOtherRules_DoesNotWarnAsync()
        {
            await new VerifyCS.Test
            {
                TestState =
                {
                    Sources =
                    {
                        $@"
                        public class C
                        {{
                            private void Test(string first)
                            {{
                                 throw new System.ArgumentNullException();
                            }}

                            private void TestFlipped(string first)
                            {{
                                 throw new System.ArgumentException(nameof(first), ""message"");
                            }}
                        }}"
                    },
                    AnalyzerConfigFiles = { ("/.editorconfig", @"root = true

[*]
dotnet_code_quality.CA2208.api_surface = public") }
                },
                MarkupOptions = MarkupOptions.UseFirstDescriptor
            }.RunAsync();
        }

        [Fact]
        public async Task ArgumentException_CorrectMessageAndParameterName_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"", ""first"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentException(""first is incorrect"", ""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentNullException_CorrectParameterName_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentNullException(""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentNullException_ParameterNameFollowedByPunctuation_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test1(string first)
                    {
                        throw new System.ArgumentNullException(""first.Length"");
                    }

                    public void Test2(string first)
                    {
                        throw new System.ArgumentNullException(""first[0]"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test1(first As String)
                       Throw New System.ArgumentNullException(""first.Length"")
                   End Sub

                   Public Sub Test2(first As String)
                       Throw New System.ArgumentNullException(""first(0)"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentNullException_ParameterNameFollowedByNonPunctuation_WarnsAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first123"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first123", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException(""first123"")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first123", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentNullException_VariableUsed_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    string str = ""Hi there"";
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(str);
                    }
                }");
        }

        [Fact]

        public async Task ArgumentNullException_NameofParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(nameof(first));
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException(NameOf(first))
                    End Sub
                End Class");
        }

        [Fact]
        public async Task ArgumentNull_CorrectParameterNameAndMessage_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first"", ""first is null"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentNullException(""first"", ""first is null"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_CorrectParameterName_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentOutOfRangeException(""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_CorrectParameterNameAndMessage_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first"", ""first is out of range"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.DuplicateWaitObjectException(""first"", ""first is out of range"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task DuplicateWaitObjectException_CorrectParameterName_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.DuplicateWaitObjectException(""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task DuplicateWaitObjectException_CorrectParameterNameAndMessage_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first"", ""first is duplicate"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.DuplicateWaitObjectException(""first"", ""first is duplicate"")
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentExceptionType_NotHavingConstructorWithParameterName_NoArgument_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.Text.DecoderFallbackException ();
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.Text.DecoderFallbackException ()
                   End Sub
               End Class");
        }

        [Fact]
        public async Task ArgumentExceptionType_NotHavingConstructor_WithParameterName_WithArgument_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.Text.DecoderFallbackException (""first"");
                    }
                }");

            await VerifyVB.VerifyAnalyzerAsync(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.Text.DecoderFallbackException (""first"")
                   End Sub
               End Class");
        }

        [Fact, WorkItem(1824, "https://github.com/dotnet/roslyn-analyzers/issues/1824")]
        public async Task ArgumentNullException_LocalFunctionParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test()
                    {
                        void Validate(string a)
                        {
                            if (a == null) throw new System.ArgumentNullException(nameof(a));
                        }
                    }
                }");
        }

        [Fact, WorkItem(1824, "https://github.com/dotnet/roslyn-analyzers/issues/1824")]
        public async Task ArgumentNullException_NestedLocalFunctionParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test()
                    {
                        void Validate(string a)
                        {
                            void ValidateNested()
                            {
                                if (a == null) throw new System.ArgumentNullException(nameof(a));
                            }
                        }
                    }
                }");
        }

        [Fact, WorkItem(1824, "https://github.com/dotnet/roslyn-analyzers/issues/1824")]
        public async Task ArgumentNullException_LambdaParameter_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test()
                    {
                        System.Action<string> lambda = a =>
                        {
                            if (a == null) throw new System.ArgumentNullException(nameof(a));
                        };
                    }
                }");
        }

        [Fact, WorkItem(1561, "https://github.com/dotnet/roslyn-analyzers/issues/1561")]
        public async Task ArgumentOutOfRangeException_PropertyName_DoesNotWarnAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                using System;

                public class Class1
                {
                    private int _size;
                    public int Size
                    {
                        get => _size;
                        set
                        {
                            if (value < 0)
                            {
                                throw new ArgumentOutOfRangeException(nameof(Size));
                            }

                            _size = value;
                        }
                    }
                }");
        }

        [Fact, WorkItem(6580, "https://github.com/dotnet/roslyn-analyzers/issues/6580")]
        public async Task ArgumentNullException_Test()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string name)
                    {
                        if (name is null) throw new System.ArgumentNullException(""not name"", ""Must not be null."");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 49, "Test", "not name", "paramName", "ArgumentNullException"));
        }

        private static DiagnosticResult GetCSharpNoArgumentsExpectedResult(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleNoArguments)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetBasicNoArgumentsExpectedResult(int line, int column, string typeName) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleNoArguments)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(typeName);

        private static DiagnosticResult GetCSharpIncorrectMessageExpectedResult(int line, int column, params string[] args) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectMessage)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetBasicIncorrectMessageExpectedResult(int line, int column, params string[] args) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectMessage)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetCSharpIncorrectParameterNameExpectedResult(int line, int column, params string[] args) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyCS.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectParameterName)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);

        private static DiagnosticResult GetBasicIncorrectParameterNameExpectedResult(int line, int column, params string[] args) =>
#pragma warning disable RS0030 // Do not use banned APIs
            VerifyVB.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectParameterName)
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(args);
    }
}