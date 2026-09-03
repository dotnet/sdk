// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = Test.Utilities.VisualBasicCodeFixVerifier<
    Microsoft.NetCore.VisualBasic.Analyzers.Runtime.BasicDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class DetectPreviewFeatureUnitTests
    {
        [Fact]
        public async Task TestUnmarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program : IProgram
            {
                static void Main(string[] args)
                {
                    new Program();
                }

                public void {|#0:MarkedMethodInInterface|}()
                {
                    throw new NotImplementedException();
                }        
            }

            public interface IProgram
            {
                [RequiresPreviewFeatures]
                void MarkedMethodInInterface();
            }
        }
            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedMethodInInterface", "IProgram.MarkedMethodInInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @"
        Imports System.Runtime.Versioning
        Imports System

        Namespace Preview_Feature_Scratch
            Class Program
                Implements IProgram

                Private Shared Sub Main(ByVal args As String())
                    Dim prog = New Program()
                End Sub

                Public Sub MarkedMethodInInterface() Implements IProgram.{|#0:MarkedMethodInInterface|}
                    Throw New NotImplementedException()
                End Sub
            End Class

            Interface IProgram
                <RequiresPreviewFeatures>
                Sub MarkedMethodInInterface()
            End Interface
        End Namespace";

            var testVb = TestVB(vbInput);
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedMethodInInterface", "IProgram.MarkedMethodInInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
            await testVb.RunAsync();
        }

        [Fact]
        public async Task TestMarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program : {|#1:IProgram|}
            {
                static void Main(string[] args)
                {
                    new Program();
                }

                public void {|#0:UnmarkedMethodInMarkedInterface|}() { }
            }

            [RequiresPreviewFeatures]
            public interface IProgram
            {
                public void UnmarkedMethodInMarkedInterface() { }
            }
        }
            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("UnmarkedMethodInMarkedInterface", "IProgram.UnmarkedMethodInMarkedInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(1).WithArguments("Program", "IProgram", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
        Imports System
        Imports System.Runtime.Versioning
        Module Preview_Feature_Scratch
            Public Class Program
                Implements {|#1:IProgram|}
                Private Shared Sub Main(ByVal args As String())
                    Dim prog = New Program()
                End Sub

                Public Sub MarkedMethodInInterface() Implements IProgram.{|#0:MarkedMethodInInterface|}
                    Throw New NotImplementedException()
                End Sub

                Public ReadOnly Property Value As String Implements IProgram.{|#2:Value|}
                    {|#3:Get|}
                        Return """"
                    End Get
                End Property
            End Class

            <RequiresPreviewFeatures>
            Public Interface IProgram
                Sub MarkedMethodInInterface()
                ReadOnly Property Value() As String 
            End Interface
        End Module
            ";

            var testVb = TestVB(vbInput);
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedMethodInInterface", "IProgram.MarkedMethodInInterface", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(1).WithArguments("Program", "IProgram", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(2).WithArguments("Value", "IProgram.Value", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(3).WithArguments("get_Value", "IProgram.get_Value", DetectPreviewFeatureAnalyzer.DefaultURL));
            await testVb.RunAsync();
        }

        [Fact]
        public async Task TestMarkedEmptyPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program : {|#0:IProgram|}
            {
                static void Main(string[] args)
                {
                    new Program();
                }
            }

            [RequiresPreviewFeatures]
            public interface IProgram
            {
            }
        }
            ";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Program", "IProgram", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
        Imports System
        Imports System.Runtime.Versioning
        Module Preview_Feature_Scratch
            Public Class Program
                Implements {|#1:IProgram|}
                Private Shared Sub Main(ByVal args As String())
                    Dim prog = New Program()
                End Sub
            End Class

            <RequiresPreviewFeatures>
            Public Interface IProgram
            End Interface
        End Module
            ";

            var testVb = TestVB(vbInput);
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(1).WithArguments("Program", "IProgram", DetectPreviewFeatureAnalyzer.DefaultURL));
            await testVb.RunAsync();
        }

        [Fact]
        public async Task TestDerivedInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    interface IZoo : {|#0:IFoo|}
    {
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Bar();
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("IZoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
        Imports System
        Imports System.Runtime.Versioning
        Module Preview_Feature_Scratch
            Public Interface IZoo
                Inherits {|#0:IFoo|}
            End Interface

            <RequiresPreviewFeatures>
            Public Interface IFoo
                Sub Bar()
            End Interface
        End Module
            ";

            var testVb = TestVB(vbInput);
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("IZoo", "IFoo", DetectPreviewFeatureAnalyzer.DefaultURL));
            await testVb.RunAsync();
        }
    }
}
