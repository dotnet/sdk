// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class ParameterNamesShouldMatchBaseDeclarationTests : DiagnosticAnalyzerTestBase
    {
        [Fact]
        public void VerifyNoFalsePositivesAreReported()
        {
            VerifyCSharp(@"public class TestClass
                           {
                               public void TestMethod() { }
                           }");

            VerifyCSharp(@"public class TestClass
                           {
                               public void TestMethod(string arg1, string arg2) { }
                           }");

            VerifyCSharp(@"public class TestClass
                           {
                               public void TestMethod(string arg1, string arg2, __arglist) { }
                           }");

            VerifyCSharp(@"public class TestClass
                           {
                               public void TestMethod(string arg1, string arg2, params string[] arg3) { }
                           }");

            VerifyBasic(@"Public Class TestClass
                              Public Sub TestMethod()
                              End Sub
                          End Class");

            VerifyBasic(@"Public Class TestClass
                              Public Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class");

            VerifyBasic(@"Public Class TestClass
                              Public Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3() As String)
                              End Sub
                          End Class");
        }

        [Fact]
        public void VerifyOverrideWithWrongParameterNames()
        {
            VerifyCSharp(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2) { }
                           }",
                         GetCSharpResultAt(8, 71, "void TestClass.TestMethod(string arg1, string arg2)", "arg1", "baseArg1", "void BaseClass.TestMethod(string baseArg1, string baseArg2)"),
                         GetCSharpResultAt(8, 84, "void TestClass.TestMethod(string arg1, string arg2)", "arg2", "baseArg2", "void BaseClass.TestMethod(string baseArg1, string baseArg2)"));

            VerifyCSharp(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           public class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2, __arglist) { }
                           }",
                         GetCSharpResultAt(8, 71, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg1", "baseArg1", "void BaseClass.TestMethod(string baseArg1, string baseArg2, __arglist)"),
                         GetCSharpResultAt(8, 84, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg2", "baseArg2", "void BaseClass.TestMethod(string baseArg1, string baseArg2, __arglist)"));

            VerifyCSharp(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                           }

                           public class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2, params string[] arg3) { }
                           }",
                         GetCSharpResultAt(8, 71, "void TestClass.TestMethod(string arg1, string arg2, params string[] arg3)", "arg1", "baseArg1", "void BaseClass.TestMethod(string baseArg1, string baseArg2, params string[] baseArg3)"),
                         GetCSharpResultAt(8, 84, "void TestClass.TestMethod(string arg1, string arg2, params string[] arg3)", "arg2", "baseArg2", "void BaseClass.TestMethod(string baseArg1, string baseArg2, params string[] baseArg3)"),
                         GetCSharpResultAt(8, 106, "void TestClass.TestMethod(string arg1, string arg2, params string[] arg3)", "arg3", "baseArg3", "void BaseClass.TestMethod(string baseArg1, string baseArg2, params string[] baseArg3)"));

            VerifyBasic(@"Public MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Class

                          Public Class TestClass 
                              Inherits BaseClass

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class",
                         GetBasicResultAt(8, 63, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String)"),
                         GetBasicResultAt(8, 79, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String)"));

            VerifyBasic(@"Public MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())
                          End Class

                          Public Class TestClass
                              Inherits BaseClass

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())
                              End Sub
                          End Class",
                         GetBasicResultAt(8, 63, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg1", "baseArg1", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"),
                         GetBasicResultAt(8, 79, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg2", "baseArg2", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"),
                         GetBasicResultAt(8, 106, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg3", "baseArg3", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VerifyInternalOverrideWithWrongParameterNames_NoDiagnostic()
        {
            VerifyCSharp(@"public abstract class BaseClass
                           {
                               internal abstract void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : BaseClass
                           {
                               internal override void TestMethod(string arg1, string arg2) { }
                           }");

            VerifyCSharp(@"internal abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           internal class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2, __arglist) { }
                           }");

            VerifyCSharp(@"internal class OuterClass
                           {
                               public abstract class BaseClass
                               {
                                   public abstract void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                               }

                               public class TestClass : BaseClass
                               {
                                   public override void TestMethod(string arg1, string arg2, params string[] arg3) { }
                               }
                           }");

            VerifyBasic(@"Friend MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Class

                          Friend Class TestClass 
                              Inherits BaseClass

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class");

            VerifyBasic(@"Public MustInherit Class BaseClass
                              Friend MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())
                          End Class

                          Public Class TestClass
                              Inherits BaseClass

                              Friend Overrides Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())
                              End Sub
                          End Class");

            VerifyBasic(@"Friend Class OuterClass
                              Public MustInherit Class BaseClass
                                  Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())
                              End Class

                              Public Class TestClass
                                  Inherits BaseClass

                                  Public Overrides Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())
                                  End Sub
                              End Class
                          End Class");
        }

        [Fact]
        public void VerifyInterfaceImplementationWithWrongParameterNames()
        {
            VerifyCSharp(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : IBase
                           {
                               public void TestMethod(string arg1, string arg2) { }
                           }",
                         GetCSharpResultAt(8, 62, "void TestClass.TestMethod(string arg1, string arg2)", "arg1", "baseArg1", "void IBase.TestMethod(string baseArg1, string baseArg2)"),
                         GetCSharpResultAt(8, 75, "void TestClass.TestMethod(string arg1, string arg2)", "arg2", "baseArg2", "void IBase.TestMethod(string baseArg1, string baseArg2)"));

            VerifyCSharp(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           public class TestClass : IBase
                           {
                               public void TestMethod(string arg1, string arg2, __arglist) { }
                           }",
                         GetCSharpResultAt(8, 62, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg1", "baseArg1", "void IBase.TestMethod(string baseArg1, string baseArg2, __arglist)"),
                         GetCSharpResultAt(8, 75, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg2", "baseArg2", "void IBase.TestMethod(string baseArg1, string baseArg2, __arglist)"));

            VerifyCSharp(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                           }

                           public class TestClass : IBase
                           {
                               public void TestMethod(string arg1, string arg2, params string[] arg3) { }
                           }",
                         GetCSharpResultAt(8, 62, "void TestClass.TestMethod(string arg1, string arg2, params string[] arg3)", "arg1", "baseArg1", "void IBase.TestMethod(string baseArg1, string baseArg2, params string[] baseArg3)"),
                         GetCSharpResultAt(8, 75, "void TestClass.TestMethod(string arg1, string arg2, params string[] arg3)", "arg2", "baseArg2", "void IBase.TestMethod(string baseArg1, string baseArg2, params string[] baseArg3)"),
                         GetCSharpResultAt(8, 97, "void TestClass.TestMethod(string arg1, string arg2, params string[] arg3)", "arg3", "baseArg3", "void IBase.TestMethod(string baseArg1, string baseArg2, params string[] baseArg3)"));

            VerifyBasic(@"Public Interface IBase
                              Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Interface

                          Public Class TestClass
                              Implements IBase

                              Public Sub TestMethod(arg1 As String, arg2 As String) Implements IBase.TestMethod
                              End Sub
                          End Class",
                        GetBasicResultAt(8, 53, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"),
                        GetBasicResultAt(8, 69, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"));

            VerifyBasic(@"Public Interface IBase
                              Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                          End Interface

                          Public Class TestClass
                              Implements IBase

                              Public Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3() As String) Implements IBase.TestMethod
                              End Sub
                          End Class",
                        GetBasicResultAt(8, 53, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg1", "baseArg1", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"),
                        GetBasicResultAt(8, 69, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg2", "baseArg2", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"),
                        GetBasicResultAt(8, 96, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg3", "baseArg3", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"));
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public void VerifyExplicitInterfaceImplementationWithWrongParameterNames_NoDiagnostic()
        {
            VerifyCSharp(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : IBase
                           {
                               void IBase.TestMethod(string arg1, string arg2) { }
                           }");

            VerifyCSharp(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           public class TestClass : IBase
                           {
                               void IBase.TestMethod(string arg1, string arg2, __arglist) { }
                           }");

            VerifyCSharp(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                           }

                           public class TestClass : IBase
                           {
                               void IBase.TestMethod(string arg1, string arg2, params string[] arg3) { }
                           }");
        }

        [Fact]
        public void VerifyInterfaceImplementationWithDifferentMethodName()
        {
            VerifyBasic(@"Public Interface IBase
                              Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Interface

                          Public Class TestClass
                              Implements IBase

                              Public Sub AnotherTestMethod(arg1 As String, arg2 As String) Implements IBase.TestMethod
                              End Sub
                          End Class",
                        GetBasicResultAt(8, 60, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"),
                        GetBasicResultAt(8, 76, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"));

            VerifyBasic(@"Public Interface IBase
                              Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())
                          End Interface

                          Public Class TestClass
                              Implements IBase

                              Public Sub AnotherTestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String()) Implements IBase.TestMethod
                              End Sub
                          End Class",
                        GetBasicResultAt(8, 60, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg1", "baseArg1", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"),
                        GetBasicResultAt(8, 76, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg2", "baseArg2", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"),
                        GetBasicResultAt(8, 103, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())", "arg3", "baseArg3", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())"));
        }

        [Fact]
        public void VerifyThatInvalidOverrideIsNotReported()
        {
            VerifyCSharp(@"public class TestClass
                           {
                               public override void TestMethod(string arg1, string arg2) { }
                           }", TestValidationMode.AllowCompileErrors);

            VerifyBasic(@"Public Class TestClass
                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class", TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void VerifyOverrideWithInheritanceChain()
        {
            VerifyCSharp(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2);
                           }

                           public abstract class IntermediateBaseClass : BaseClass
                           {
                           }

                           public class TestClass : IntermediateBaseClass
                           {
                               public override void TestMethod(string arg1, string arg2) { }
                           }",
                         GetCSharpResultAt(12, 71, "void TestClass.TestMethod(string arg1, string arg2)", "arg1", "baseArg1", "void BaseClass.TestMethod(string baseArg1, string baseArg2)"),
                         GetCSharpResultAt(12, 84, "void TestClass.TestMethod(string arg1, string arg2)", "arg2", "baseArg2", "void BaseClass.TestMethod(string baseArg1, string baseArg2)"));

            VerifyBasic(@"Public MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Class

                          Public MustInherit Class IntermediateBaseClass
                              Inherits BaseClass
                          End Class

                          Public Class TestClass
                              Inherits IntermediateBaseClass

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class",
                         GetBasicResultAt(12, 63, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String)"),
                         GetBasicResultAt(12, 79, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String)"));
        }

        [Fact]
        public void VerifyNewOverrideWithInheritance()
        {
            VerifyCSharp(@"public class BaseClass
                           {
                               public void TestMethod(string baseArg1, string baseArg2) { }
                           }

                           public class TestClass : BaseClass
                           {
                               public new void TestMethod(string arg1, string arg2) { }
                           }");

            VerifyBasic(@"Public Class BaseClass
                              Public Sub TestMethod(baseArg1 As String, baseArg2 As String)
                              End Sub
                          End Class

                          Public Class TestClass
                              Inherits BaseClass

                              Public Shadows Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class");
        }

        [Fact]
        public void VerifyBaseClassNameHasPriority()
        {
            VerifyCSharp(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string arg1, string arg2);
                           }

                           public interface ITest
                           {
                               void TestMethod(string interfaceArg1, string interfaceArg2);
                           }

                           public class TestClass : BaseClass, ITest
                           {
                               public override void TestMethod(string arg1, string arg2) { }
                           }");

            VerifyCSharp(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string arg1, string arg2);
                           }

                           public interface ITest
                           {
                               void TestMethod(string interfaceArg1, string interfaceArg2);
                           }

                           public class TestClass : BaseClass, ITest
                           {
                               public override void TestMethod(string interfaceArg1, string interfaceArg2) { }
                           }",
                         GetCSharpResultAt(13, 71, "void TestClass.TestMethod(string interfaceArg1, string interfaceArg2)", "interfaceArg1", "arg1", "void BaseClass.TestMethod(string arg1, string arg2)"),
                         GetCSharpResultAt(13, 93, "void TestClass.TestMethod(string interfaceArg1, string interfaceArg2)", "interfaceArg2", "arg2", "void BaseClass.TestMethod(string arg1, string arg2)"));

            VerifyBasic(@"Public MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(arg1 As String, arg2 As String)
                          End Class

                          Public Interface ITest
                              Sub TestMethod(interfaceArg1 As String, interfaceArg2 As String)
                          End Interface

                          Public Class TestClass
                              Inherits BaseClass
                              Implements ITest

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String) Implements ITest.TestMethod
                              End Sub
                          End Class");

            VerifyBasic(@"Public MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(arg1 As String, arg2 As String)
                          End Class

                          Public Interface ITest
                              Sub TestMethod(interfaceArg1 As String, interfaceArg2 As String)
                          End Interface

                          Public Class TestClass
                              Inherits BaseClass
                              Implements ITest

                              Public Overrides Sub TestMethod(interfaceArg1 As String, interfaceArg2 As String) Implements ITest.TestMethod
                              End Sub
                          End Class",
                       GetBasicResultAt(13, 63, "Sub TestClass.TestMethod(interfaceArg1 As String, interfaceArg2 As String)", "interfaceArg1", "arg1", "Sub BaseClass.TestMethod(arg1 As String, arg2 As String)"),
                       GetBasicResultAt(13, 88, "Sub TestClass.TestMethod(interfaceArg1 As String, interfaceArg2 As String)", "interfaceArg2", "arg2", "Sub BaseClass.TestMethod(arg1 As String, arg2 As String)"));
        }

        [Fact]
        public void VerifyMultipleClashingInterfacesWithFullMatch()
        {
            VerifyCSharp(@"public interface ITest1
                           {
                               void TestMethod(string arg1, string arg2);
                           }

                           public interface ITest2
                           {
                               void TestMethod(string otherArg1, string otherArg2);
                           }

                           public class TestClass : ITest1, ITest2
                           {
                               public void TestMethod(string arg1, string arg2) { }
                           }");

            VerifyBasic(@"Public Interface ITest1
                              Sub TestMethod(arg1 As String, arg2 As String)
                          End Interface

                          Public Interface ITest2
                              Sub TestMethod(otherArg1 As String, otherArg2 As String)
                          End Interface

                          Public Class TestClass
                              Implements ITest1, ITest2

                              Public Sub TestMethod(arg1 As String, arg2 As String) Implements ITest1.TestMethod, ITest2.TestMethod
                              End Sub
                          End Class");
        }

        [Fact]
        public void VerifyMultipleClashingInterfacesWithPartialMatch()
        {
            VerifyCSharp(@"public interface ITest1
                           {
                               void TestMethod(string arg1, string arg2, string arg3);
                           }

                           public interface ITest2
                           {
                               void TestMethod(string otherArg1, string otherArg2, string otherArg3);
                           }

                           public class TestClass : ITest1, ITest2
                           {
                               public void TestMethod(string arg1, string arg2, string otherArg3) { }
                           }",
                         GetCSharpResultAt(13, 88, "void TestClass.TestMethod(string arg1, string arg2, string otherArg3)", "otherArg3", "arg3", "void ITest1.TestMethod(string arg1, string arg2, string arg3)"));

            VerifyBasic(@"Public Interface ITest1
                              Sub TestMethod(arg1 As String, arg2 As String, arg3 As String)
                          End Interface

                          Public Interface ITest2
                              Sub TestMethod(otherArg1 As String, otherArg2 As String, otherArg3 As String)
                          End Interface

                          Public Class TestClass
                              Implements ITest1, ITest2

                              Public Sub TestMethod(arg1 As String, arg2 As String, otherArg3 As String) Implements ITest1.TestMethod, ITest2.TestMethod
                              End Sub
                          End Class",
                         GetBasicResultAt(12, 85, "Sub TestClass.TestMethod(arg1 As String, arg2 As String, otherArg3 As String)", "otherArg3", "arg3", "Sub ITest1.TestMethod(arg1 As String, arg2 As String, arg3 As String)"));
        }

        [Fact]
        public void VerifyIgnoresPropertiesWithTheSameName()
        {
            VerifyCSharp(@"public interface ITest1
                           {
                               void TestMethod(string arg1, string arg2);
                           }

                           public interface ITest2
                           {
                               int TestMethod { get; set; }
                           }

                           public class TestClass : ITest1, ITest2
                           {
                               public void TestMethod(string arg1, string arg2) { }
                               int ITest2.TestMethod { get; set; }
                           }");

            VerifyBasic(@"Public Interface ITest1
                              Sub TestMethod(arg1 As String, arg2 As String)
                          End Interface

                          Public Interface ITest2
                              Property TestMethod As Integer
                          End Interface

                          Public Class TestClass
                              Implements ITest1, ITest2

                              Public Sub TestMethod(arg1 As String, arg2 As String) Implements ITest1.TestMethod
                              End Sub

                              Private Property TestMethodFromITest2 As Integer Implements ITest2.TestMethod
                          End Class");
        }

        [Fact]
        public void VerifyHandlesMultipleBaseMethodsWithTheSameName()
        {
            VerifyCSharp(@"public interface ITest
                           {
                               void TestMethod(string arg1);
                               void TestMethod(string arg1, string arg2);
                           }

                           public class TestClass : ITest
                           {
                               public void TestMethod(string arg1) { }
                               public void TestMethod(string arg1, string arg2) { }
                           }");

            VerifyBasic(@"Public Interface ITest
                              Sub TestMethod(arg1 As String)
                              Sub TestMethod(arg1 As String, arg2 As String)
                          End Interface

                          Public Class TestClass
                              Implements ITest

                              Public Sub TestMethod(arg1 As String) Implements ITest.TestMethod
                              End Sub

                              Public Sub TestMethod(arg1 As String, arg2 As String) Implements ITest.TestMethod
                              End Sub
                          End Class");
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string violatingMember, string violatingParameter, string baseParameter, string baseMember)
        {
            return GetCSharpResultAt(line, column, ParameterNamesShouldMatchBaseDeclarationAnalyzer.Rule, violatingMember, violatingParameter, baseParameter, baseMember);
        }

        private static DiagnosticResult GetBasicResultAt(int line, int column, string violatingMember, string violatingParameter, string baseParameter, string baseMember)
        {
            return GetBasicResultAt(line, column, ParameterNamesShouldMatchBaseDeclarationAnalyzer.Rule, violatingMember, violatingParameter, baseParameter, baseMember);
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new ParameterNamesShouldMatchBaseDeclarationAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ParameterNamesShouldMatchBaseDeclarationAnalyzer();
        }
    }
}