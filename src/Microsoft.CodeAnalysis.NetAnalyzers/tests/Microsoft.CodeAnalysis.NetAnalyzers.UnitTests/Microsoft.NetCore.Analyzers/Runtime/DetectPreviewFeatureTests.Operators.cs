// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        public async Task TestPreviewMethodUnaryOperator()
        {
            var csInput = @" 
using System.Runtime.Versioning; using System;
namespace Preview_Feature_Scratch
{
    public class Program
    {
        static void Main(string[] args)
        {
            var a = new Fraction();
            var b = {|#0:+a|};
        }
    }

    public readonly struct Fraction
    {
        [RequiresPreviewFeatures]
        public static Fraction operator +(Fraction a) => a;
    }
}
";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("op_UnaryPlus", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewMethodBinaryOperator()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {
            public class Program
            {
                static void Main(string[] args)
                {
                    var a = new Fraction();
                    var b = new Fraction();
                    b = {|#0:b + a|};
                }
            }

            public readonly struct Fraction
            {
                [RequiresPreviewFeatures]
                public static Fraction operator +(Fraction a, Fraction b) => a;
            }
        }
        ";
            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("op_Addition", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }
    }
}
