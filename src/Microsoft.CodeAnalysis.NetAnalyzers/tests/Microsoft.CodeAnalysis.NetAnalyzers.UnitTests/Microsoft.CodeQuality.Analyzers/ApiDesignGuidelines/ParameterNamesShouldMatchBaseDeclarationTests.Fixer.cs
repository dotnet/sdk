// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
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
    public class ParameterNamesShouldMatchBaseDeclarationFixerTests
    {
        [Fact]
        public async Task VerifyOverrideWithWrongParameterNamesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2);
                              }

                              public class TestClass : BaseClass
                              {
                                  public override void TestMethod(string [|arg1|], string [|arg2|]) { }
                              }",
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2);
                              }

                              public class TestClass : BaseClass
                              {
                                  public override void TestMethod(string baseArg1, string baseArg2) { }
                              }");

            await VerifyCS.VerifyCodeFixAsync(
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2, __arglist);
                              }

                              public class TestClass : BaseClass
                              {
                                  public override void TestMethod(string [|arg1|], string [|arg2|], __arglist) { }
                              }",
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2, __arglist);
                              }

                              public class TestClass : BaseClass
                              {
                                  public override void TestMethod(string baseArg1, string baseArg2, __arglist) { }
                              }");

            await VerifyCS.VerifyCodeFixAsync(
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                              }

                              public class TestClass : BaseClass
                              {
                                  public override void TestMethod(string [|arg1|], string [|arg2|], params string[] [|arg3|]) { }
                              }",
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                              }

                              public class TestClass : BaseClass
                              {
                                  public override void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3) { }
                              }");

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Class

                             Public Class TestClass 
                                 Inherits BaseClass

                                 Public Overrides Sub TestMethod([|arg1|] as String, [|arg2|] as String)
                                 End Sub
                             End Class",
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Class

                             Public Class TestClass 
                                 Inherits BaseClass

                                 Public Overrides Sub TestMethod(baseArg1 as String, baseArg2 as String)
                                 End Sub
                             End Class");

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                             End Class

                             Public Class TestClass
                                 Inherits BaseClass

                                 Public Overrides Sub TestMethod([|arg1|] as String, [|arg2|] as String, ParamArray [|arg3|]() As String)
                                 End Sub
                             End Class",
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                             End Class

                             Public Class TestClass
                                 Inherits BaseClass

                                 Public Overrides Sub TestMethod(baseArg1 as String, baseArg2 as String, ParamArray baseArg3() As String)
                                 End Sub
                             End Class");
        }

        [Fact]
        public async Task VerifyInterfaceImplementationWithWrongParameterNamesAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
                        @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2);
                              }

                              public class TestClass : IBase
                              {
                                  public void TestMethod(string [|arg1|], string [|arg2|]) { }
                              }",
                        @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2);
                              }

                              public class TestClass : IBase
                              {
                                  public void TestMethod(string baseArg1, string baseArg2) { }
                              }");

            await VerifyCS.VerifyCodeFixAsync(
                        @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2, __arglist);
                              }

                              public class TestClass : IBase
                              {
                                  public void TestMethod(string [|arg1|], string [|arg2|], __arglist) { }
                              }",
                        @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2, __arglist);
                              }

                              public class TestClass : IBase
                              {
                                  public void TestMethod(string baseArg1, string baseArg2, __arglist) { }
                              }");

            await VerifyCS.VerifyCodeFixAsync(
                        @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                              }

                              public class TestClass : IBase
                              {
                                  public void TestMethod(string [|arg1|], string [|arg2|], params string[] [|arg3|]) { }
                              }",
                        @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                              }

                              public class TestClass : IBase
                              {
                                  public void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3) { }
                              }");

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub TestMethod([|arg1|] As String, [|arg2|] As String) Implements IBase.TestMethod
                                 End Sub
                             End Class",
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub TestMethod(baseArg1 As String, baseArg2 As String) Implements IBase.TestMethod
                                 End Sub
                             End Class");

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub TestMethod([|arg1|] As String, [|arg2|] As String, ParamArray [|arg3|]() As String) Implements IBase.TestMethod
                                 End Sub
                             End Class",
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String) Implements IBase.TestMethod
                                 End Sub
                             End Class");
        }

        [Fact, WorkItem(1432, "https://github.com/dotnet/roslyn-analyzers/issues/1432")]
        public async Task VerifyExplicitInterfaceImplementationWithWrongParameterNames_NoDiagnosticAsync()
        {
            var source = @"public interface IBase
                              {
                                  void TestMethod(string baseArg1, string baseArg2);
                              }

                              public class TestClass : IBase
                              {
                                  void IBase.TestMethod(string arg1, string arg2) { }
                              }";
            await VerifyCS.VerifyCodeFixAsync(source, source);

            source = @"public interface IBase
                        {
                            void TestMethod(string baseArg1, string baseArg2, __arglist);
                        }

                        public class TestClass : IBase
                        {
                            void IBase.TestMethod(string arg1, string arg2, __arglist) { }
                        }";
            await VerifyCS.VerifyCodeFixAsync(source, source);

            source = @"public interface IBase
                        {
                            void TestMethod(string baseArg1, string baseArg2, params string[] baseArg3);
                        }

                        public class TestClass : IBase
                        {
                            void IBase.TestMethod(string arg1, string arg2, params string[] arg3) { }
                        }";
            await VerifyCS.VerifyCodeFixAsync(source, source);
        }

        [Fact]
        public async Task VerifyInterfaceImplementationWithDifferentMethodNameAsync()
        {
            await VerifyVB.VerifyCodeFixAsync(
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub AnotherTestMethod([|arg1|] As String, [|arg2|] As String) Implements IBase.TestMethod
                                 End Sub
                             End Class",
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub AnotherTestMethod(baseArg1 As String, baseArg2 As String) Implements IBase.TestMethod
                                 End Sub
                             End Class");

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub AnotherTestMethod([|arg1|] As String, [|arg2|] As String, ParamArray [|arg3|]() As String) Implements IBase.TestMethod
                                 End Sub
                             End Class",
                        @"Public Interface IBase
                                 Sub TestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String)
                             End Interface

                             Public Class TestClass
                                 Implements IBase

                                 Public Sub AnotherTestMethod(baseArg1 As String, baseArg2 As String, ParamArray baseArg3() As String) Implements IBase.TestMethod
                                 End Sub
                             End Class");
        }

        [Fact]
        public async Task VerifyOverrideWithInheritanceChainAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2);
                              }

                              public abstract class IntermediateBaseClass : BaseClass
                              {
                              }

                              public class TestClass : IntermediateBaseClass
                              {
                                  public override void TestMethod(string [|arg1|], string [|arg2|]) { }
                              }",
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string baseArg1, string baseArg2);
                              }

                              public abstract class IntermediateBaseClass : BaseClass
                              {
                              }

                              public class TestClass : IntermediateBaseClass
                              {
                                  public override void TestMethod(string baseArg1, string baseArg2) { }
                              }");

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Class

                             Public MustInherit Class IntermediateBaseClass
                                 Inherits BaseClass
                             End Class

                             Public Class TestClass
                                 Inherits IntermediateBaseClass

                                 Public Overrides Sub TestMethod([|arg1|] As String, [|arg2|] As String)
                                 End Sub
                             End Class",
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(baseArg1 As String, baseArg2 As String)
                             End Class

                             Public MustInherit Class IntermediateBaseClass
                                 Inherits BaseClass
                             End Class

                             Public Class TestClass
                                 Inherits IntermediateBaseClass

                                 Public Overrides Sub TestMethod(baseArg1 As String, baseArg2 As String)
                                 End Sub
                             End Class");
        }

        [Fact]
        public async Task VerifyBaseClassNameHasPriorityAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(
                        @"public abstract class BaseClass
                              {
                                  public abstract void TestMethod(string arg1, string arg2);
                              }

                              public interface ITest
                              {
                                  void TestMethod(string interfaceArg1, string interfaceArg2);
                              }

                              public class TestClass : BaseClass, ITest
                              {
                                  public override void TestMethod(string [|interfaceArg1|], string [|interfaceArg2|]) { }
                              }",
                        @"public abstract class BaseClass
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

            await VerifyVB.VerifyCodeFixAsync(
                        @"Public MustInherit Class BaseClass
                                 Public MustOverride Sub TestMethod(arg1 As String, arg2 As String)
                             End Class
    
                             Public Interface ITest
                                 Sub TestMethod(interfaceArg1 As String, interfaceArg2 As String)
                             End Interface
    
                             Public Class TestClass
                                 Inherits BaseClass
                                 Implements ITest
    
                                 Public Overrides Sub TestMethod([|interfaceArg1|] As String, [|interfaceArg2|] As String) Implements ITest.TestMethod
                                 End Sub
                             End Class",
                        @"Public MustInherit Class BaseClass
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
        }

        [Fact]
        public async Task VerifyMultipleClashingInterfacesWithPartialMatchAsync()
        {
            await VerifyCS.VerifyCodeFixAsync(@"public interface ITest1
                              {
                                  void TestMethod(string arg1, string arg2, string arg3);
                              }

                              public interface ITest2
                              {
                                  void TestMethod(string otherArg1, string otherArg2, string otherArg3);
                              }

                              public class TestClass : ITest1, ITest2
                              {
                                  public void TestMethod(string arg1, string arg2, string [|otherArg3|]) { }
                              }",
                            @"public interface ITest1
                              {
                                  void TestMethod(string arg1, string arg2, string arg3);
                              }

                              public interface ITest2
                              {
                                  void TestMethod(string otherArg1, string otherArg2, string otherArg3);
                              }

                              public class TestClass : ITest1, ITest2
                              {
                                  public void TestMethod(string arg1, string arg2, string arg3) { }
                              }");

            await VerifyVB.VerifyCodeFixAsync(@"Public Interface ITest1
                                 Sub TestMethod(arg1 As String, arg2 As String, arg3 As String)
                             End Interface
    
                             Public Interface ITest2
                                 Sub TestMethod(otherArg1 As String, otherArg2 As String, otherArg3 As String)
                             End Interface
    
                             Public Class TestClass
                                 Implements ITest1, ITest2
    
                                 Public Sub TestMethod(arg1 As String, arg2 As String, [|otherArg3|] As String) Implements ITest1.TestMethod, ITest2.TestMethod
                                 End Sub
                             End Class",
                            @"Public Interface ITest1
                                 Sub TestMethod(arg1 As String, arg2 As String, arg3 As String)
                             End Interface
    
                             Public Interface ITest2
                                 Sub TestMethod(otherArg1 As String, otherArg2 As String, otherArg3 As String)
                             End Interface
    
                             Public Class TestClass
                                 Implements ITest1, ITest2
    
                                 Public Sub TestMethod(arg1 As String, arg2 As String, arg3 As String) Implements ITest1.TestMethod, ITest2.TestMethod
                                 End Sub
                             End Class");
        }
    }
}