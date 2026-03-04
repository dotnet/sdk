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
        [Theory]
        [InlineData("assembly")]
        [InlineData("module")]
        public async Task TestAssemblyDoesntUsePreviewDependency(string assemblyOrModule)
        {
            // No diagnostic when we don't use any APIs from an assembly marked with Preview
            string csCurrentAssemblyCode = @"
using System;

public class Program
{
    public void ProgramMethod()
    {
        new Program();
    }
}";
            string csDepedencyCode = @$"[{assemblyOrModule}: System.Runtime.Versioning.RequiresPreviewFeatures]";

            var test = SetupDependencyAndTestCSWithOneSourceFile(csCurrentAssemblyCode, csDepedencyCode);
            await test.RunAsync();
        }

        [Theory]
        [InlineData("assembly")]
        [InlineData("module")]
        public async Task TestCallAPIsFromAssemblyMarkedAsPreview(string assemblyOrModule)
        {
            string csDependencyCode = @"
public class Library
{
    public void AMethod() { }
    private int _property;
    public int AProperty 
    {
        get => 1;
        set
        {
            _property = value;
        }
    }
}";
            csDependencyCode = @$"[{assemblyOrModule}: System.Runtime.Versioning.RequiresPreviewFeatures] {csDependencyCode}";

            string csCurrentAssemblyCode = @"
using System;

public class Program
{
    public void ProgramMethod()
    {
        Library library = {|#1:new Library()|};

        {|#0:library.AMethod()|};
        int prop = {|#2:library.AProperty|};
    }
}";
            var test = SetupDependencyAndTestCSWithOneSourceFile(csCurrentAssemblyCode, csDependencyCode);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("AMethod", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("Library", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(2).WithArguments("AProperty", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Theory]
        [InlineData("assembly")]
        [InlineData("module")]
        public async Task TestNoCallsToPreviewDependency(string assemblyOrModule)
        {
            string csDependencyCode = @"
public class Library
{
    public void AMethod() { }
    private int _property;
    public int AProperty 
    {
        get => 1;
        set
        {
            _property = value;
        }
    }
}";
            csDependencyCode = @$"[{assemblyOrModule}: System.Runtime.Versioning.RequiresPreviewFeatures] {csDependencyCode}";

            string csCurrentAssemblyCode = @"
using System;

public class Program
{
    public void ProgramMethod()
    {
    }
}";
            var test = SetupDependencyAndTestCSWithOneSourceFile(csCurrentAssemblyCode, csDependencyCode);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMixtureOfPreviewAPIsInDependency()
        {
            string csDependencyCode = @"
public class Library
{
    public void AMethod() 
    {
#pragma warning disable CA2252
        APreviewMethod();
#pragma warning enable CA2252
    }

    [System.Runtime.Versioning.RequiresPreviewFeatures]
    public void APreviewMethod() { }
}";

            string csCurrentAssemblyCode = @"
using System;

public class Program
{
    public void ProgramMethod()
    {
        Library library = new Library();

        library.AMethod();
        {|#0:library.APreviewMethod()|};
    }
}";
            var test = SetupDependencyAndTestCSWithOneSourceFile(csCurrentAssemblyCode, csDependencyCode);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("APreviewMethod", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDeepNestingOfPreviewAPIsInDependency()
        {
            string csDependencyCode = @"
public class Library
{
    [System.Runtime.Versioning.RequiresPreviewFeatures]
    public class NestedClass0
    {
        public class NestedClass1
        {
            public class NestedClass2
            {
                public class NestedClass3
                {
                    public void APreviewMethod() { }
                }
            }
        }
    }
}";

            string csCurrentAssemblyCode = @"
using System;

public class Program
{
    public void ProgramMethod()
    {
        Library.NestedClass0.NestedClass1.NestedClass2.NestedClass3 nestedClass = {|#0:new()|};

        {|#1:nestedClass.APreviewMethod()|};
    }
}";
            var test = SetupDependencyAndTestCSWithOneSourceFile(csCurrentAssemblyCode, csDependencyCode);
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(0).WithArguments("NestedClass3", DetectPreviewFeatureAnalyzer.DefaultURL));
            test.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(DetectPreviewFeatureAnalyzer.GeneralPreviewFeatureAttributeRule).WithLocation(1).WithArguments("APreviewMethod", DetectPreviewFeatureAnalyzer.DefaultURL));
            await test.RunAsync();
        }
    }
}