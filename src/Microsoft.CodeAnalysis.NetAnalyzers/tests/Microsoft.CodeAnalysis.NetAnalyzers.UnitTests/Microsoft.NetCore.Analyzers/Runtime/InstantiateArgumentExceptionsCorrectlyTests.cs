// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class InstantiateArgumentExceptionsCorrectlyTests : DiagnosticAnalyzerTestBase
    {
        private static readonly string s_noArguments = MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageNoArguments;
        private static readonly string s_incorrectMessage = MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageIncorrectMessage;
        private static readonly string s_incorrectParameterName = MicrosoftNetCoreAnalyzersResources.InstantiateArgumentExceptionsCorrectlyMessageIncorrectParameterName;

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new InstantiateArgumentExceptionsCorrectlyAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new InstantiateArgumentExceptionsCorrectlyAnalyzer();
        }

        [Fact]
        public void ArgumentException_NoArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException();
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_noArguments, "ArgumentException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException()
                    End Sub
                End Class",
              GetBasicExpectedResult(4, 31, s_noArguments, "ArgumentException"));
        }

        [Fact]
        public void ArgumentException_EmptyParameterNameArgument_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException("""");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "", "paramName", "ArgumentNullException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException("""")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public void ArgumentNullException_SpaceParameterArgument_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException("" "");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", " ", "paramName", "ArgumentNullException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException("" "")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", " ", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public void ArgumentNullException_NameofNonParameter_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        var foo = new object();
                        throw new System.ArgumentNullException(nameof(foo));
                    }
                }",
                GetCSharpExpectedResult(7, 31, s_incorrectParameterName, "Test", "foo", "paramName", "ArgumentNullException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Dim foo As New Object()
                        Throw New System.ArgumentNullException(NameOf(foo))
                    End Sub
                End Class",
                GetBasicExpectedResult(5, 31, s_incorrectParameterName, "Test", "foo", "paramName", "ArgumentNullException"));
        }

        [Fact]
        public void ArgumentException_ParameterNameAsMessage_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException(""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"));
        }

        [Fact]
        public void ArgumentException_ReversedArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first"", ""first is incorrect"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"),
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is incorrect", "paramName", "ArgumentException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentException(""first"", ""first is incorrect"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentException"),
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is incorrect", "paramName", "ArgumentException"));
        }

        [Fact]
        public void ArgumentNullException_NoArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException();
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_noArguments, "ArgumentNullException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException()
                    End Sub
                End Class",
                 GetBasicExpectedResult(4, 31, s_noArguments, "ArgumentNullException"));
        }

        [Fact]
        public void ArgumentNullException_MessageAsParameterName_Warns()
        {
            VerifyCSharp(@"
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
        public void ArgumentNullException_ReversedArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first is null"", ""first"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is null", "paramName", "ArgumentNullException"),
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentNullException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException(""first is null"", ""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is null", "paramName", "ArgumentNullException"),
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentNullException"));
        }

        [Fact]
        public void ArgumentOutOfRangeException_NoArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException();
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_noArguments, "ArgumentOutOfRangeException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException()
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_noArguments, "ArgumentOutOfRangeException"));
        }

        [Fact]
        public void ArgumentOutOfRangeException_MessageAsParameterName_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first is out of range"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException(""first is out of range"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"));
        }

        [Fact]
        public void ArgumentOutOfRangeException_ReversedArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first is out of range"", ""first"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"),
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentOutOfRangeException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentOutOfRangeException(""first is out of range"", ""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is out of range", "paramName", "ArgumentOutOfRangeException"),
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "ArgumentOutOfRangeException"));
        }

        [Fact]
        public void DuplicateWaitObjectException_NoArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException();
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_noArguments, "DuplicateWaitObjectException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException()
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_noArguments, "DuplicateWaitObjectException"));
        }

        [Fact]
        public void DuplicateWaitObjectException_MessageAsParameterName_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first is duplicate"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException(""first is duplicate"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"));
        }

        [Fact]
        public void DuplicateWaitObjectException_ReversedArguments_Warns()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first is duplicate"", ""first"");
                    }
                }",
                GetCSharpExpectedResult(6, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"),
                GetCSharpExpectedResult(6, 31, s_incorrectMessage, "Test", "first", "message", "DuplicateWaitObjectException"));

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.DuplicateWaitObjectException(""first is duplicate"", ""first"")
                    End Sub
                End Class",
                GetBasicExpectedResult(4, 31, s_incorrectParameterName, "Test", "first is duplicate", "parameterName", "DuplicateWaitObjectException"),
                GetBasicExpectedResult(4, 31, s_incorrectMessage, "Test", "first", "message", "DuplicateWaitObjectException"));
        }


        [Fact]
        public void ArgumentException_CorrectMessage_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentException(""first is incorrect"")
                   End Sub
               End Class");
        }

        [Fact]
        public void ArgumentException_CorrectMessageAndParameterName_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentException(""first is incorrect"", ""first"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentException(""first is incorrect"", ""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public void ArgumentNullException_CorrectParameterName_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentNullException(""first"")
                   End Sub
               End Class");
        }


        [Fact]

        public void ArgumentNullException_NameofParameter_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(nameof(first));
                    }
                }");

            VerifyBasic(@"
                Public Class [MyClass]
                    Public Sub Test(first As String)
                        Throw New System.ArgumentNullException(NameOf(first))
                    End Sub
                End Class");
        }

        [Fact]
        public void ArgumentNull_CorrectParameterNameAndMessage_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentNullException(""first"", ""first is null"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentNullException(""first"", ""first is null"")
                   End Sub
               End Class");
        }

        [Fact]
        public void ArgumentOutOfRangeException_CorrectParameterName_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.ArgumentOutOfRangeException(""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public void ArgumentOutOfRangeException_CorrectParameterNameAndMessage_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.ArgumentOutOfRangeException(""first"", ""first is out of range"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.DuplicateWaitObjectException(""first"", ""first is out of range"")
                   End Sub
               End Class");
        }

        [Fact]
        public void DuplicateWaitObjectException_CorrectParameterName_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.DuplicateWaitObjectException(""first"")
                   End Sub
               End Class");
        }

        [Fact]
        public void DuplicateWaitObjectException_CorrectParameterNameAndMessage_DoesNotWarn()
        {
            VerifyCSharp(@"
                public class Class
                {
                    public void Test(string first)
                    {
                        throw new System.DuplicateWaitObjectException(""first"", ""first is duplicate"");
                    }
                }");

            VerifyBasic(@"
               Public Class [MyClass]
                   Public Sub Test(first As String)
                       Throw New System.DuplicateWaitObjectException(""first"", ""first is duplicate"")
                   End Sub
               End Class");
        }

        [Fact, WorkItem(1824, "https://github.com/dotnet/roslyn-analyzers/issues/1824")]
        public void ArgumentNullException_LocalFunctionParameter_DoesNotWarn()
        {
            VerifyCSharp(@"
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
        public void ArgumentNullException_NestedLocalFunctionParameter_DoesNotWarn()
        {
            VerifyCSharp(@"
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
        public void ArgumentNullException_LambdaParameter_DoesNotWarn()
        {
            VerifyCSharp(@"
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
        public void ArgumentOutOfRangeException_PropertyName_DoesNotWarn()
        {
            VerifyCSharp(@"
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
            string message = string.Format(format, args);
            return GetCSharpResultAt(line, column, InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleId, message);
        }

        private static DiagnosticResult GetBasicExpectedResult(int line, int column, string format, params string[] args)
        {
            string message = string.Format(format, args);
            return GetBasicResultAt(line, column, InstantiateArgumentExceptionsCorrectlyAnalyzer.RuleId, message);
        }
    }
}