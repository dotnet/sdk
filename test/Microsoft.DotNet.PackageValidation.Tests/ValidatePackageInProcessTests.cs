// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.PackageValidation.Validators;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class ValidatePackageInProcessTests : SdkTest
    {
        public ValidatePackageInProcessTests(ITestOutputHelper log) : base(log)
        {
        }

        private (SuppressibleTestLog, CompatibleFrameworkInPackageValidator) CreateLoggerAndValidator()
        {
            SuppressibleTestLog log = new();
            CompatibleFrameworkInPackageValidator validator = new(log,
                new ApiCompatRunner(log,
                    new SuppressionEngine(),
                    new ApiComparerFactory(new RuleFactory(log)),
                    new AssemblySymbolLoaderFactory(log)));

            return (log, validator);
        }

        [Fact]
        public void ValidatePackageWithReferences()
        {
            string testDependencySource = @"namespace PackageValidationTests { public class IntermediateBaseClass
#if NETSTANDARD2_0
: IBaseInterface
#endif
{ } }";

            TestProject testSubDependency = CreateTestProject(@"namespace PackageValidationTests { public interface IBaseInterface { } }", "netstandard2.0");
            TestProject testDependency = CreateTestProject(
                                            testDependencySource,
                                            $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}",
                                            new[] { testSubDependency });
            TestProject testProject = CreateTestProject(@"namespace PackageValidationTests { public class First : IntermediateBaseClass { } }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { testDependency });

            TestAsset asset = TestAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);
            Package package = Package.Create(packCommand.GetNuGetPackage());
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            // First we run without references. Without references, ApiCompat should not be able to see that class First
            // removed an interface due to it's base class removing that implementation. We validate that APICompat doesn't
            // log errors when not using references.
            validator.Validate(new PackageValidatorOption(package));
            Assert.Empty(log.errors);

            // Now we do pass in references. With references, ApiCompat should now detect that an interface was removed in a
            // dependent assembly, causing one of our types to stop implementing that assembly. We validate that a CP0008 is logged.
            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            package = Package.Create(packCommand.GetNuGetPackage(), references);
            validator.Validate(new PackageValidatorOption(package));
            Assert.NotEmpty(log.errors);

            Assert.Contains($"CP0008 Type 'PackageValidationTests.First' does not implement interface 'PackageValidationTests.IBaseInterface' on lib/{ToolsetInfo.CurrentTargetFramework}/{asset.TestProject.Name}.dll but it does on lib/netstandard2.0/{asset.TestProject.Name}.dll", log.errors);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ValidateOnlyErrorWhenAReferenceIsRequired(bool createDependencyToDummy, bool useReferences)
        {
            string testDependencyCode = createDependencyToDummy ?
                                        @"namespace PackageValidationTests{public class SomeBaseClass : IDummyInterface { }public class SomeDummyClass : IDummyInterface { }}" :
                                        @"namespace PackageValidationTests{public class SomeBaseClass { }public class SomeDummyClass : IDummyInterface { }}";

            TestProject testDummyDependency = CreateTestProject(@"namespace PackageValidationTests { public interface IDummyInterface { } }", "netstandard2.0");
            TestProject testDependency = CreateTestProject(testDependencyCode, "netstandard2.0", new[] { testDummyDependency });
            TestProject testProject = CreateTestProject(@"namespace PackageValidationTests { public class First : SomeBaseClass { } }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { testDependency });

            TestAsset asset = TestAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), packageAssemblyReferences: useReferences ? references : null);

            File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{testDummyDependency.Name}.dll"));
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            // The test name reflects the original intent: ApiCompat should only flag a missing reference (CP1002)
            // when the type graph being compared actually requires that reference at runtime. Today CP1002 is
            // temporarily downgraded to an informational message (see https://github.com/dotnet/sdk/issues/46236),
            // so no errors or warnings should be produced for any combination in the matrix.
            validator.Validate(new PackageValidatorOption(package));
            Assert.Empty(log.errors);
            Assert.DoesNotContain(log.warnings, e => e.Contains("CP1002"));
        }

        [Theory]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ValidateErrorWhenTypeForwardingReferences(bool useReferences, bool expectCP0001, bool deleteFile)
        {
            string dependencySourceCode = @"namespace PackageValidationTests { public interface ISomeInterface { }
#if !NETSTANDARD2_0
public class MyForwardedType : ISomeInterface { }
#endif
}";
            string testSourceCode = @"
#if NETSTANDARD2_0
namespace PackageValidationTests { public class MyForwardedType : ISomeInterface { } }
#else
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(PackageValidationTests.MyForwardedType))]
#endif";
            TestProject dependency = CreateTestProject(dependencySourceCode, $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}");
            TestProject testProject = CreateTestProject(testSourceCode, $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}", new[] { dependency });

            TestAsset asset = TestAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseFolder(ToolsetInfo.CurrentTargetFramework), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework) } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), packageAssemblyReferences: useReferences ? references : null);

            if (deleteFile)
                File.Delete(Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", ToolsetInfo.CurrentTargetFramework, $"{dependency.Name}.dll"));

            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            if (expectCP0001)
                Assert.Contains($"CP0001 Type 'PackageValidationTests.MyForwardedType' exists on lib/netstandard2.0/{testProject.Name}.dll but not on lib/{ToolsetInfo.CurrentTargetFramework}/{testProject.Name}.dll", log.errors);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ValidateMissingReferencesIsOnlyLoggedWhenRunningWithReferences(bool useReferences)
        {
            TestProject testProject = CreateTestProject("public class MyType { }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}");
            TestAsset asset = TestAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Equal(string.Empty, result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.ParseFolder("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), useReferences ? references : null);
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            if (!useReferences)
                Assert.DoesNotContain(log.warnings, e => e.Contains("CP1003"));
            else
                Assert.Contains(log.warnings, e => e.Contains("CP1003"));
        }

        [Fact]
        public void ValidateReferencesAreRespectedForPlatformSpecificTFMs()
        {
            TestProject testProject = CreateTestProject("public class MyType { }", $"netstandard2.0;{ToolsetInfo.CurrentTargetFramework}-windows");
            TestAsset asset = TestAssetsManager.CreateTestProject(testProject, testProject.Name);
            PackCommand packCommand = new(Log, Path.Combine(asset.TestRoot, testProject.Name));
            var result = packCommand.Execute();
            Assert.Empty(result.StdErr);

            Dictionary<NuGetFramework, IEnumerable<string>> references = new()
            {
                { NuGetFramework.Parse("netstandard2.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", "netstandard2.0") } },
                { NuGetFramework.ParseComponents($".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}", "Windows,Version=7.0"), new string[] { Path.Combine(asset.TestRoot, asset.TestProject.Name, "bin", "Debug", $"net{ToolsetInfo.CurrentTargetFrameworkVersion}-windows") } }
            };
            Package package = Package.Create(packCommand.GetNuGetPackage(), references);
            (SuppressibleTestLog log, CompatibleFrameworkInPackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package));

            Assert.DoesNotContain(log.warnings, e => e.Contains("CP1003"));
        }

        private static TestProject CreateTestProject(string sourceCode, string tfms, IEnumerable<TestProject> referenceProjects = null)
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            TestProject testProject = new()
            {
                Name = name,
                TargetFrameworks = tfms,
            };

            testProject.SourceFiles.Add($"{name}.cs", sourceCode);

            if (referenceProjects != null)
            {
                foreach (var project in referenceProjects)
                    testProject.ReferencedProjects.Add(project);
            }

            return testProject;
        }
    }
}
