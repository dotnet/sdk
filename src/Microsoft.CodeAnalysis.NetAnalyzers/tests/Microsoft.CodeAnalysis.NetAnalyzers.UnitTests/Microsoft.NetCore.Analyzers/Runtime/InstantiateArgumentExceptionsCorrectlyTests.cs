// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.InstantiateArgumentExceptionsCorrectlyAnalyzer,
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpInstantiateArgumentExceptionsCorrectlyFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.InstantiateArgumentExceptionsCorrectlyAnalyzer,
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicInstantiateArgumentExceptionsCorrectlyFixer>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class InstantiateArgumentExceptionsCorrectlyTests
    {
        [Fact]
        public async Task ArgumentException_NoArguments_Warns()
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
        public async Task ArgumentException_EmptyParameterNameArgument_Warns()
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
        public async Task ArgumentNullException_SpaceParameterArgument_Warns()
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
        public async Task ArgumentNullException_NameofNonParameter_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        var foo = new object();
                        throw new System.ArgumentNullException(nameof(foo));
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(7, 31, "Test", "foo", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Dim foo As New Object()
                        Throw New System.ArgumentNullException(NameOf(foo))
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(5, 31, "Test", "foo", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentException_ParameterNameAsMessage_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException(""first"")
                    End Sub
                End Class",
                GetBasicIncorrectMessageExpectedResult(4, 31, "Test", "first", "message", "ArgumentException"));
        }

        [Fact]
        public async Task ArgumentException_ReversedArguments_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"", ""first is incorrect"");
                    }
                }",
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentException"),
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is incorrect", "paramName", "ArgumentException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException(""first"", ""first is incorrect"")
                    End Sub
                End Class",
                GetBasicIncorrectMessageExpectedResult(4, 31, "Test", "first", "message", "ArgumentException"),
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first is incorrect", "paramName", "ArgumentException"));
        }

        [Fact]
        public async Task ArgumentNullException_NoArguments_Warns()
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
        public async Task ArgumentNullException_MessageAsParameterName_Warns()
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
        public async Task ArgumentNullException_ReversedArguments_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first is null"", ""first"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is null", "paramName", "ArgumentNullException"),
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException(""first is null"", ""first"")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first is null", "paramName", "ArgumentNullException"),
                GetBasicIncorrectMessageExpectedResult(4, 31, "Test", "first", "message", "ArgumentNullException"));
        }

        [Fact]
        public async Task ArgumentOutOfRangeException_NoArguments_Warns()
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
        public async Task ArgumentOutOfRangeException_MessageAsParameterName_Warns()
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
        public async Task ArgumentOutOfRangeException_ReversedArguments_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first is out of range"", ""first"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"),
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "ArgumentOutOfRangeException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException(""first is out of range"", ""first"")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"),
                GetBasicIncorrectMessageExpectedResult(4, 31, "Test", "first", "message", "ArgumentOutOfRangeException"));
        }

        [Fact]
        public async Task DuplicateWaitObjectException_NoArguments_Warns()
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
        public async Task DuplicateWaitObjectException_MessageAsParameterName_Warns()
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
        public async Task DuplicateWaitObjectException_ReversedArguments_Warns()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first is duplicate"", ""first"");
                    }
                }",
                GetCSharpIncorrectParameterNameExpectedResult(6, 31, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"),
                GetCSharpIncorrectMessageExpectedResult(6, 31, "Test", "first", "message", "DuplicateWaitObjectException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException(""first is duplicate"", ""first"")
                    End Sub
                End Class",
                GetBasicIncorrectParameterNameExpectedResult(4, 31, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"),
                GetBasicIncorrectMessageExpectedResult(4, 31, "Test", "first", "message", "DuplicateWaitObjectException"));
        }


        [Fact]
        public async Task ArgumentException_CorrectMessage_DoesNotWarn()
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

        [Fact]
        public async Task ArgumentException_CorrectMessageAndParameterName_DoesNotWarn()
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
        public async Task ArgumentNullException_CorrectParameterName_DoesNotWarn()
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

        public async Task ArgumentNullException_NameofParameter_DoesNotWarn()
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
        public async Task ArgumentNull_CorrectParameterNameAndMessage_DoesNotWarn()
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
        public async Task ArgumentOutOfRangeException_CorrectParameterName_DoesNotWarn()
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
        public async Task ArgumentOutOfRangeException_CorrectParameterNameAndMessage_DoesNotWarn()
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
        public async Task DuplicateWaitObjectException_CorrectParameterName_DoesNotWarn()
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
        public async Task DuplicateWaitObjectException_CorrectParameterNameAndMessage_DoesNotWarn()
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

        [Fact, WorkItem(1824, "https://github.com/dotnet/roslyn-analyzers/issues/1824")]
        public async Task ArgumentNullException_LocalFunctionParameter_DoesNotWarn()
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
        public async Task ArgumentNullException_NestedLocalFunctionParameter_DoesNotWarn()
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
        public async Task ArgumentNullException_LambdaParameter_DoesNotWarn()
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
        public async Task ArgumentOutOfRangeException_PropertyName_DoesNotWarn()
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

        private static DiagnosticResult GetCSharpNoArgumentsExpectedResult(int line, int column, string typeName) =>
            VerifyCS.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleNoArguments)
                .WithLocation(line, column)
                .WithArguments(typeName);

        private static DiagnosticResult GetBasicNoArgumentsExpectedResult(int line, int column, string typeName) =>
            VerifyVB.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleNoArguments)
                .WithLocation(line, column)
                .WithArguments(typeName);

        private static DiagnosticResult GetCSharpIncorrectMessageExpectedResult(int line, int column, params string[] args) =>
            VerifyCS.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectMessage)
                .WithLocation(line, column)
                .WithArguments(args);

        private static DiagnosticResult GetBasicIncorrectMessageExpectedResult(int line, int column, params string[] args) =>
            VerifyVB.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectMessage)
                .WithLocation(line, column)
                .WithArguments(args);

        private static DiagnosticResult GetCSharpIncorrectParameterNameExpectedResult(int line, int column, params string[] args) =>
            VerifyCS.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectParameterName)
                .WithLocation(line, column)
                .WithArguments(args);

        private static DiagnosticResult GetBasicIncorrectParameterNameExpectedResult(int line, int column, params string[] args) =>
            VerifyVB.Diagnostic(InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleIncorrectParameterName)
                .WithLocation(line, column)
                .WithArguments(args);
    }
}