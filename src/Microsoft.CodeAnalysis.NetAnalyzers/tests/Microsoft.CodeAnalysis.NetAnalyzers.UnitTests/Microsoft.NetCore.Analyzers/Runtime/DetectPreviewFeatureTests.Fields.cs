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
        public async Task TestDictionaryFieldWithPreviewType()
        {
            var csInput = @" 
        using System.Runtime.Versioning; using System;
        using System.Collections.Generic;
        namespace Preview_Feature_Scratch
        {

            class Program
            {
                private Dictionary<int, {|#0:PreviewType|}> _genericPreviewFieldDictionary;
#nullable enable
                private Dictionary<int, {|#3:PreviewType|}?>? _genericPreviewFieldDictionaryWithNullable;
#nullable disable
                private List<List<List<List<{|#1:PreviewType|}>>>> _genericPreviewField;
                private List<List<AGenericClass<Int32>>> _genericClassField;
                private List<List<{|#2:AGenericPreviewClass<Int32>|}>> _genericPreviewClassField;


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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_genericPreviewFieldDictionary", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(3).WithArguments("_genericPreviewFieldDictionaryWithNullable", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(1).WithArguments("_genericPreviewField", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewClassField", "AGenericPreviewClass", DetectPreviewFeatureAnalyzer.DefaultURL));
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
                private {|#0:PreviewType|} _field;
                private AGenericClass<{|#2:PreviewType|}> _genericPreviewField;
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
        Imports System
        Imports System.Runtime.Versioning
        Imports System.Collections.Generic
        Module Preview_Feature_Scratch
            Public Class Program
                Private _field As {|#0:PreviewType|}
                Private _genericPreviewField As AGenericClass(Of {|#2:PreviewType|})
                Private _noDiagnosticField As AGenericClass(Of Boolean)
            End Class

            <RequiresPreviewFeatures>
            Public Class PreviewType
            End Class

            Public Class AGenericClass(Of T)
            End Class

        End Module
            ";

            var testVb = TestVB(vbInput);
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            testVb.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            await testVb.RunAsync();
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
                private {|#0:PreviewType|} _field;
                private {|#4:PreviewType|}[] _fieldArray;
                private {|#5:PreviewType|}[][] _fieldArrayOfArray;
                private AGenericClass<{|#2:PreviewType|}>[] _genericPreviewField;

                public Program()
                {
                    _field = {|#1:new PreviewType()|};
                } 

                static void Main(string[] args)
                {
                }
            }

            [RequiresPreviewFeatures(""Lib is in preview."", Url = ""https://aka.ms/aspnet/kestrel/http3reqs"")]
            public class PreviewType
            {

            }

            public class AGenericClass<T>
                where T : {|#3:PreviewType|}
            {

            }

#nullable enable
            public class AGenericClassWithNullable<T>
                where T : {|#6:PreviewType?|}
            {
                private {|#7:PreviewType|}? _fieldNullable;
                private {|#8:PreviewType|}[]? _fieldArrayNullable;
                private {|#9:PreviewType|}[][]? _fieldArrayOfArrayNullable;
            }
#nullable disable
        }";

            var test = TestCS(csInput);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(0).WithArguments("_field", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRuleWithCustomMessage).WithLocation(1).WithArguments("PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(3).WithArguments("AGenericClass", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(4).WithArguments("_fieldArray", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(5).WithArguments("_fieldArrayOfArray", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.UsesPreviewTypeParameterRuleWithCustomMessage).WithLocation(6).WithArguments("AGenericClassWithNullable", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(7).WithArguments("_fieldNullable", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(8).WithArguments("_fieldArrayNullable", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRuleWithCustomMessage).WithLocation(9).WithArguments("_fieldArrayOfArrayNullable", "PreviewType", "https://aka.ms/aspnet/kestrel/http3reqs", "Lib is in preview."));
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
                private {|#0:PreviewType|} _field;
                private {|#3:AGenericClass<{|#2:PreviewType|}>|}[] _genericPreviewField;

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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(3).WithArguments("_genericPreviewField", "AGenericClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();

            var vbInput = @" 
        Imports System.Runtime.Versioning
        Imports System

        Namespace Preview_Feature_Scratch
            Public Class Program
                Private _field As {|#0:PreviewType|}
                Private _genericPreviewField As {|#3:AGenericClass(Of {|#2:PreviewType|})|}()

                Public Sub New()
                    _field = {|#1:New PreviewType()|}
                End Sub

                Private Shared Sub Main(ByVal args As String())
                End Sub
            End Class

            <RequiresPreviewFeatures>
            Public Class PreviewType
            End Class

            <RequiresPreviewFeatures>
            Public Class AGenericClass(Of T As PreviewType)
            End Class
        End Namespace";

            var vbTest = TestVB(vbInput);
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(0).WithArguments("_field", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(2).WithArguments("_genericPreviewField", "PreviewType", DetectPreviewFeatureAnalyzer.DefaultURL));
            vbTest.ExpectedDiagnostics.Add(VerifyVB.Diagnostic(DetectPreviewFeatureAnalyzer.FieldOrEventIsPreviewTypeRule).WithLocation(3).WithArguments("_genericPreviewField", "AGenericClass", DetectPreviewFeatureAnalyzer.DefaultURL));
            await vbTest.RunAsync();
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
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("_field", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }
    }
}
