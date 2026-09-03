// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ParameterNamesShouldMatchBaseDeclarationAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ParameterNamesShouldMatchBaseDeclarationFixer>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ParameterNamesShouldMatchBaseDeclarationAnalyzer,
    Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.ParameterNamesShouldMatchBaseDeclarationFixer>;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines.UnitTests
{
    public class ParameterNamesShouldMatchBaseDeclarationTests
    {
        [Fact]
        public async Task VerifyNoFalsePositivesAreReportedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public class TestClass
                           {
                               public void TestMethod() { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"public class TestClass
                           {
                               public void TestMethod(string arg1, string arg2) { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"public class TestClass
                           {
                               public void TestMethod(string arg1, string arg2, __arglist) { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"public class TestClass
                           {
                               public void TestMethod(string arg1, string arg2, params string[] arg3) { }
                           }");

            await VerifyVB.VerifyAnalyzerAsync(@"Public Class TestClass
                              Public Sub TestMethod()
                              End Sub
                          End Class");

            await VerifyVB.VerifyAnalyzerAsync(@"Public Class TestClass
                              Public Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class");

            await VerifyVB.VerifyAnalyzerAsync(@"Public Class TestClass
                              Public Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3() As String)
                              End Sub
                          End Class");
        }

        [Fact]
        public async Task VerifyOverrideWithWrongParameterNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2) { }
                           }",
                         GetCSharpResultAt(8, 71, "void TestClass.TestMethod(string arg1, string arg2)", "arg1", "baseArg1", "void BaseClass.TestMethod(string baseArg1, string baseArg2)"),
                         GetCSharpResultAt(8, 84, "void TestClass.TestMethod(string arg1, string arg2)", "arg2", "baseArg2", "void BaseClass.TestMethod(string baseArg1, string baseArg2)"));

            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           public class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2, __arglist) { }
                           }",
                         GetCSharpResultAt(8, 71, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg1", "baseArg1", "void BaseClass.TestMethod(string baseArg1, string baseArg2, __arglist)"),
                         GetCSharpResultAt(8, 84, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg2", "baseArg2", "void BaseClass.TestMethod(string baseArg1, string baseArg2, __arglist)"));

            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Class

                          Public Class TestClass
                              Inherits BaseClass

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class",
                         GetBasicResultAt(8, 63, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String)"),
                         GetBasicResultAt(8, 79, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub BaseClass.TestMethod(baseArg1 As String, baseArg2 As String)"));

            await VerifyVB.VerifyAnalyzerAsync(@"Public MustInherit Class BaseClass
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
        public async Task VerifyInternalOverrideWithWrongParameterNames_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
                           {
                               internal abstract void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : BaseClass
                           {
                               internal override void TestMethod(string arg1, string arg2) { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"internal abstract class BaseClass
                           {
                               public abstract void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           internal class TestClass : BaseClass
                           {
                               public override void TestMethod(string arg1, string arg2, __arglist) { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"internal class OuterClass
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

            await VerifyVB.VerifyAnalyzerAsync(@"Friend MustInherit Class BaseClass
                              Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Class

                          Friend Class TestClass 
                              Inherits BaseClass

                              Public Overrides Sub TestMethod(arg1 As String, arg2 As String)
                              End Sub
                          End Class");

            await VerifyVB.VerifyAnalyzerAsync(@"Public MustInherit Class BaseClass
                              Friend MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3 As String())
                          End Class

                          Public Class TestClass
                              Inherits BaseClass

                              Friend Overrides Sub TestMethod(arg1 As String, arg2 As String, ParamArray arg3 As String())
                              End Sub
                          End Class");

            await VerifyVB.VerifyAnalyzerAsync(@"Friend Class OuterClass
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
        public async Task VerifyInterfaceImplementationWithWrongParameterNamesAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : IBase
                           {
                               public void TestMethod(string arg1, string arg2) { }
                           }",
                         GetCSharpResultAt(8, 62, "void TestClass.TestMethod(string arg1, string arg2)", "arg1", "baseArg1", "void IBase.TestMethod(string baseArg1, string baseArg2)"),
                         GetCSharpResultAt(8, 75, "void TestClass.TestMethod(string arg1, string arg2)", "arg2", "baseArg2", "void IBase.TestMethod(string baseArg1, string baseArg2)"));

            await VerifyCS.VerifyAnalyzerAsync(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           public class TestClass : IBase
                           {
                               public void TestMethod(string arg1, string arg2, __arglist) { }
                           }",
                         GetCSharpResultAt(8, 62, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg1", "baseArg1", "void IBase.TestMethod(string baseArg1, string baseArg2, __arglist)"),
                         GetCSharpResultAt(8, 75, "void TestClass.TestMethod(string arg1, string arg2, __arglist)", "arg2", "baseArg2", "void IBase.TestMethod(string baseArg1, string baseArg2, __arglist)"));

            await VerifyCS.VerifyAnalyzerAsync(@"public interface IBase
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface IBase
                              Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Interface

                          Public Class TestClass
                              Implements IBase

                              Public Sub TestMethod(arg1 As String, arg2 As String) Implements IBase.TestMethod
                              End Sub
                          End Class",
                        GetBasicResultAt(8, 53, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"),
                        GetBasicResultAt(8, 69, "Sub TestClass.TestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"));

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface IBase
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
        public async Task VerifyExplicitInterfaceImplementationWithWrongParameterNames_NoDiagnosticAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2);
                           }

                           public class TestClass : IBase
                           {
                               void IBase.TestMethod(string arg1, string arg2) { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, __arglist);
                           }

                           public class TestClass : IBase
                           {
                               void IBase.TestMethod(string arg1, string arg2, __arglist) { }
                           }");

            await VerifyCS.VerifyAnalyzerAsync(@"public interface IBase
                           {
                               void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                           }

                           public class TestClass : IBase
                           {
                               void IBase.TestMethod(string arg1, string arg2, params string[] arg3) { }
                           }");
        }

        [Fact]
        public async Task VerifyInterfaceImplementationWithDifferentMethodNameAsync()
        {
            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface IBase
                              Sub TestMethod(baseArg1 As String, baseArg2 As String)
                          End Interface

                          Public Class TestClass
                              Implements IBase

                              Public Sub AnotherTestMethod(arg1 As String, arg2 As String) Implements IBase.TestMethod
                              End Sub
                          End Class",
                        GetBasicResultAt(8, 60, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String)", "arg1", "baseArg1", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"),
                        GetBasicResultAt(8, 76, "Sub TestClass.AnotherTestMethod(arg1 As String, arg2 As String)", "arg2", "baseArg2", "Sub IBase.TestMethod(baseArg1 As String, baseArg2 As String)"));

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface IBase
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
        public async Task VerifyThatInvalidOverrideIsNotReportedAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public class TestClass
                           {
                               public override void {|CS0115:TestMethod|}(string arg1, string arg2) { }
                           }");

            await VerifyVB.VerifyAnalyzerAsync(@"Public Class TestClass
                              Public Overrides Sub {|BC30284:TestMethod|}(arg1 As String, arg2 As String)
                              End Sub
                          End Class");
        }

        [Fact]
        public async Task VerifyOverrideWithInheritanceChainAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public MustInherit Class BaseClass
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
        public async Task VerifyNewOverrideWithInheritanceAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public class BaseClass
                           {
                               public void TestMethod(string baseArg1, string baseArg2) { }
                           }

                           public class TestClass : BaseClass
                           {
                               public new void TestMethod(string arg1, string arg2) { }
                           }");

            await VerifyVB.VerifyAnalyzerAsync(@"Public Class BaseClass
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
        public async Task VerifyBaseClassNameHasPriorityAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
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

            await VerifyCS.VerifyAnalyzerAsync(@"public abstract class BaseClass
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public MustInherit Class BaseClass
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public MustInherit Class BaseClass
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
        public async Task VerifyMultipleClashingInterfacesWithFullMatchAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public interface ITest1
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface ITest1
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
        public async Task VerifyMultipleClashingInterfacesWithPartialMatchAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public interface ITest1
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface ITest1
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
        public async Task VerifyIgnoresPropertiesWithTheSameNameAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public interface ITest1
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

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface ITest1
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
        public async Task VerifyHandlesMultipleBaseMethodsWithTheSameNameAsync()
        {
            await VerifyCS.VerifyAnalyzerAsync(@"public interface ITest
                           {
                               void TestMethod(string arg1);
                               void TestMethod(string arg1, string arg2);
                           }

                           public class TestClass : ITest
                           {
                               public void TestMethod(string arg1) { }
                               public void TestMethod(string arg1, string arg2) { }
                           }");

            await VerifyVB.VerifyAnalyzerAsync(@"Public Interface ITest
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

        [Fact]
        public Task DontWarnWhenImplementingGenericParameterInInterface()
        {
            const string code = """
                                public interface IService<TEntity>
                                {
                                    void Update(TEntity entity);
                                }
                                
                                public class User {}
                                
                                public class UserService : IService<User>
                                {
                                    public void Update(User user) {}
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task DontWarnWhenImplementingGenericParameterInBaseClass()
        {
            const string code = """
                                public class BaseService<TEntity>
                                {
                                    public virtual void Update(TEntity entity) {}
                                }

                                public class User {}

                                public class UserService : BaseService<User>
                                {
                                    public override void Update(User user) {}
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        [Fact]
        public Task DontWarnWhenImplementingGenericParameterInAbstractBaseClass()
        {
            const string code = """
                                public abstract class BaseService<TEntity>
                                {
                                    public abstract void Update(TEntity entity);
                                }

                                public class User {}

                                public class UserService : BaseService<User>
                                {
                                    public override void Update(User user) {}
                                }
                                """;

            return VerifyCS.VerifyAnalyzerAsync(code);
        }

        private static DiagnosticResult GetCSharpResultAt(int line, int column, string violatingMember, string violatingParameter, string baseParameter, string baseMember)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyCS.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(violatingMember, violatingParameter, baseParameter, baseMember);

        private static DiagnosticResult GetBasicResultAt(int line, int column, string violatingMember, string violatingParameter, string baseParameter, string baseMember)
#pragma warning disable RS0030 // Do not use banned APIs
            => VerifyVB.Diagnostic()
                .WithLocation(line, column)
#pragma warning restore RS0030 // Do not use banned APIs
                .WithArguments(violatingMember, violatingParameter, baseParameter, baseMember);
    }
}