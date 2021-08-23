// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Runtime.DetectPreviewFeatureAnalyzer,
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("MarkedMethodInInterface", "IProgram.MarkedMethodInInterface"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMarkedPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class {|#1:Program|} : IProgram
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewMethodRule).WithLocation(0).WithArguments("UnmarkedMethodInMarkedInterface", "IProgram.UnmarkedMethodInMarkedInterface"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(1).WithArguments("Program", "IProgram"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMarkedEmptyPreviewInterface()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class {|#0:Program|} : IProgram
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("Program", "IProgram"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDerivedInterface()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{

    interface {|#0:IZoo|} : IFoo
    {
    }

    [RequiresPreviewFeatures]
    interface IFoo
    {
        void Bar();
    }
}";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.ImplementsPreviewInterfaceRule).WithLocation(0).WithArguments("IZoo", "IFoo"));
            await test.RunAsync();
        }
    }
}
