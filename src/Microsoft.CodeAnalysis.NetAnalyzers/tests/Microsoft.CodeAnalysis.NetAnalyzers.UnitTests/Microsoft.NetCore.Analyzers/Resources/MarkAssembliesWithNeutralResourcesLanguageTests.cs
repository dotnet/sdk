// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.NetCore.CSharp.Analyzers.Resources;
using Microsoft.NetCore.VisualBasic.Analyzers.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Resources.UnitTests
{
    public class MarkAssembliesWithNeutralResourcesLanguageTests : DiagnosticAnalyzerTestBase
    {
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new BasicMarkAssembliesWithNeutralResourcesLanguageAnalyzer();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new CSharpMarkAssembliesWithNeutralResourcesLanguageAnalyzer();
        }

        private const string CSharpDesignerFile = @"
namespace DesignerFile {
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")]
    internal class Resource1 { }
}";

        private const string BasicDesignerFile = @"
Namespace My.Resources
    <Global.System.CodeDom.Compiler.GeneratedCodeAttribute(""System.Resources.Tools.StronglyTypedResourceBuilder"", ""4.0.0.0"")> _
    Friend Class Resource1
    End Class
End Namespace";

        [Fact]
        public void TestCSharpNoResourceFile()
        {
            VerifyCSharp(@"class C {}");
        }

        [Fact]
        public void TestBasicNoResourceFile()
        {
            VerifyBasic(@"Class C
End Class");
        }

        [Fact]
        public void TestCSharpResourceFile()
        {
            VerifyCSharp(GetSources(@"class C {}", LanguageNames.CSharp), GetGlobalResult(MarkAssembliesWithNeutralResourcesLanguageAnalyzer.Rule));
        }

        [Fact]
        public void TestBasicResourceFile()
        {
            VerifyBasic(GetSources(@"Class C
End Class", LanguageNames.VisualBasic), GetGlobalResult(MarkAssembliesWithNeutralResourcesLanguageAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharpInvalidAttribute1()
        {
            VerifyCSharp(GetSources(@"[assembly: System.Resources.NeutralResourcesLanguage("""")]", LanguageNames.CSharp), GetCSharpResultAt(1, 12, MarkAssembliesWithNeutralResourcesLanguageAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharpInvalidAttribute2()
        {
            VerifyCSharp(GetSources(@"[assembly: System.Resources.NeutralResourcesLanguage(null)]", LanguageNames.CSharp), GetCSharpResultAt(1, 12, MarkAssembliesWithNeutralResourcesLanguageAnalyzer.Rule));
        }

        [Fact]
        public void TestBasicInvalidAttribute1()
        {
            VerifyBasic(GetSources(@"<Assembly: System.Resources.NeutralResourcesLanguage("""")>", LanguageNames.VisualBasic), GetBasicResultAt(1, 2, MarkAssembliesWithNeutralResourcesLanguageAnalyzer.Rule));
        }

        [Fact]
        public void TestBasicInvalidAttribute2()
        {
            VerifyBasic(GetSources(@"<Assembly: System.Resources.NeutralResourcesLanguage(Nothing)>", LanguageNames.VisualBasic), GetBasicResultAt(1, 2, MarkAssembliesWithNeutralResourcesLanguageAnalyzer.Rule));
        }

        [Fact]
        public void TestCSharpvalidAttribute()
        {
            VerifyCSharp(GetSources(@"[assembly: System.Resources.NeutralResourcesLanguage(""en"")]", LanguageNames.CSharp));
        }

        [Fact]
        public void TestBasicvalidAttribute()
        {
            VerifyBasic(GetSources(@"<Assembly: System.Resources.NeutralResourcesLanguage(""en"")>", LanguageNames.VisualBasic));
        }

        private FileAndSource[] GetSources(string code, string language)
        {
            return new[] { GetDesignerFile(language), new FileAndSource { FilePath = null, Source = code } };
        }

        private FileAndSource GetDesignerFile(string language)
        {
            return new FileAndSource
            {
                FilePath = "Test.Designer" + (language == LanguageNames.CSharp ? ".cs" : ".vb"),
                Source = language == LanguageNames.CSharp ? CSharpDesignerFile : BasicDesignerFile
            };
        }
    }
}