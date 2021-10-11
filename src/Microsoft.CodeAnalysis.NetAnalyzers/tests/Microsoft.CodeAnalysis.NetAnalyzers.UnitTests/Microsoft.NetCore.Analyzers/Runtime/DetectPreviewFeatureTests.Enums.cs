// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.CSharp.Analyzers.Runtime.CSharpDetectPreviewFeatureAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public partial class DetectPreviewFeatureUnitTests
    {
        [Fact]
        public async Task TestEnumValue()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            enum AnEnum
            {
                [RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
                Foo,
                Bar
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                    AnEnum fooEnum = {|#0:AnEnum.Foo|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRuleWithCustomMessage).WithLocation(0).WithArguments("Foo", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEnumValue_NoDiagnostic()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            enum AnEnum
            {
                Foo,
                [RequiresPreviewFeatures]
                Bar
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                    AnEnum fooEnum = AnEnum.Foo;
                }
            }
        }";

            var test = TestCS(csInput);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestEnum()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            [RequiresPreviewFeatures]
            enum AnEnum
            {
                Foo,
                Bar
            }

            class Program
            {
                public Program()
                {
                }

                static void Main(string[] args)
                {
                    AnEnum fooEnum = {|#0:AnEnum.Foo|};
                    AnEnum barEnum = {|#1:AnEnum.Bar|};
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("Foo", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("Bar", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }
    }
}
