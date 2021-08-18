// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
        public async Task TestDictionaryFieldWithPreviewType()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        using System.Collections.Generic;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                private Dictionary<int, PreviewType> {|#0:_genericPreviewFieldDictionary|};
                private List<List<List<List<PreviewType>>>> {|#1:_genericPreviewField|};
                private List<List<AGenericClass<Int32>>> _genericClassField;
                private List<List<AGenericPreviewClass<Int32>>> {|#2:_genericPreviewClassField|};


                public Program()
                {
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            class PreviewType
            {

            }

            class AGenericClass<T>
            {
            }

            [RequiresPreviewFeatures]
            class AGenericPreviewClass<T>
            {
            }

        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_genericPreviewFieldDictionary", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(1).WithArguments("_genericPreviewField", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewClassField", "AGenericPreviewClass"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewTypeField()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                private PreviewType {|#0:_field|};
                private AGenericClass<PreviewType> {|#2:_genericPreviewField|};
                private AGenericClass<bool> _noDiagnosticField;

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            class PreviewType
            {

            }

            class AGenericClass<T>
            {

            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestArrayOfGenericPreviewFields()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            public class Program
            {
                private PreviewType {|#0:_field|};
                private PreviewType[] {|#4:_fieldArray|};
                private PreviewType[][] {|#5:_fieldArrayOfArray|};
                private AGenericClass<PreviewType>[] {|#2:_genericPreviewField|};

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            public class PreviewType
            {

            }

            public class {|#3:AGenericClass|}<T>
                where T : PreviewType
            {

            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRule).WithLocation(3).WithArguments("AGenericClass", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(4).WithArguments("_fieldArray", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(5).WithArguments("_fieldArrayOfArray", "PreviewType"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPreviewTypeArrayOfGenericPreviewFields()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            public class Program
            {
                private PreviewType {|#0:_field|};
                private AGenericClass<PreviewType>[] {|#3:{|#2:_genericPreviewField|}|};

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures]
            public class PreviewType
            {

            }

            [RequiresPreviewFeatures]
            public class AGenericClass<T>
                where T : PreviewType
            {

            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType"));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(3).WithArguments("_genericPreviewField", "AGenericClass"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestField()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                [RequiresPreviewFeatures]
                private bool _field;

                public Program()
                {
                    {|#0:_field|} = true;
                } 

                static void Main(string[] args)
                {
                }
            }
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("_field"));
            await test.RunAsync();
        }
    }
}
