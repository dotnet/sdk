// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Globalization;
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
        private static readonly string s_noArguments = MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageNoArguments;
        private static readonly string s_incorrectMessage = MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageIncorrectMessage;
        private static readonly string s_incorrectParameterName = MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageIncorrectParameterName;

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
                GetCSharpExpectedResult(6, 31, s_noArguments, "ArgumentException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException()
                    End Sub
                End Class",
              GetBasicExpectedResult(4, 31, s_noArguments, "ArgumentException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException("""")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "", "paramName", "ArgumentNullException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", " ", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException("" "")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", " ", "paramName", "ArgumentNullException"));
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
                GetCSharpExpectedResult(7, 31, s_incorrectParameterName, "Test", "foo", "paramName", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Dim foo As New Object()
                        Throw New System.ArgumentNullException(NameOf(foo))
                    End Sub
                End Class",
                GetBasicExpectedResult(5, 31, s_incorrectParameterName, "Test", "foo", "paramName", "ArgumentNullException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException(""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"),
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is incorrect", "paramName", "ArgumentException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException(""first"", ""first is incorrect"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"),
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is incorrect", "paramName", "ArgumentException"));
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
                GetCSharpExpectedResult(6, 31, s_noArguments, "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException()
                    End Sub
                End Class",
                 GetBasicExpectedResult(4, 31, s_noArguments, "ArgumentNullException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is null", "paramName", "ArgumentNullException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is null", "paramName", "ArgumentNullException"),
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentNullException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException(""first is null"", ""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is null", "paramName", "ArgumentNullException"),
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentNullException"));
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
                GetCSharpExpectedResult(6, 31, s_noArguments, "ArgumentOutOfRangeException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException()
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_noArguments, "ArgumentOutOfRangeException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException(""first is out of range"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"),
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentOutOfRangeException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException(""first is out of range"", ""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"),
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentOutOfRangeException"));
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
                GetCSharpExpectedResult(6, 31, s_noArguments, "DuplicateWaitObjectException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException()
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_noArguments, "DuplicateWaitObjectException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException(""first is duplicate"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"));
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
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"),
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "DuplicateWaitObjectException"));

            await VerifyVB.VerifyAnalyzerAsync(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException(""first is duplicate"", ""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"),
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "DuplicateWaitObjectException"));
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

        private static DiagnosticResult GetCSharpExpectedResult(int line, int column, string format, params string[] args)
        {
            string message = string.Format(CultureInfo.CurrentCulture, format, args);
            return new DiagnosticResult(InstantiateArgumentExceptionsCorrectlyAnalyzer.Descriptor)
                .WithLocation(line, column)
                .WithMessage(message);
        }

        private static DiagnosticResult GetBasicExpectedResult(int line, int column, string format, params string[] args)
        {
            string message = string.Format(CultureInfo.CurrentCulture, format, args);
            return new DiagnosticResult(InstantiateArgumentExceptionsCorrectlyAnalyzer.Descriptor)
                .WithLocation(line, column)
                .WithMessage(message);
        }
    }
}