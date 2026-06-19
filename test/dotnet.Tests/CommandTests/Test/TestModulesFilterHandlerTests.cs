// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class TestModulesFilterHandlerTests : SdkTest
    {
        public TestModulesFilterHandlerTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void GetMatchedModulePaths_NullOrEmptyRootDirectory_ReturnsEmpty()
        {
            TestModulesFilterHandler.GetMatchedModulePaths("**/*.dll", null).Should().BeEmpty();
            TestModulesFilterHandler.GetMatchedModulePaths("**/*.dll", string.Empty).Should().BeEmpty();
        }

        [Fact]
        public void GetMatchedModulePaths_TrimsWhitespaceBetweenPatterns()
        {
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_TrimsWhitespaceBetweenPatterns)).Path;
            File.WriteAllText(Path.Combine(root, "A.Foo.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "B.Foo.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Other.dll"), string.Empty);

            var matches = TestModulesFilterHandler.GetMatchedModulePaths(" A.Foo.dll ; B.Foo.dll ", root);

            matches.Select(Path.GetFileName)
                .Should().BeEquivalentTo("A.Foo.dll", "B.Foo.dll");
        }

        [Fact]
        public void GetMatchedModulePaths_FoldedYamlStyleExpression_AllPatternsApplied()
        {
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_FoldedYamlStyleExpression_AllPatternsApplied)).Path;
            File.WriteAllText(Path.Combine(root, "Some.ModuleA.Tests.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Some.ModuleB.Tests.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Some.ModuleC.Tests.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Other.dll"), string.Empty);

            // The folded YAML style produces line breaks/spaces between patterns; trimming should make them work.
            var expression = "*.ModuleA.Tests.dll;\n      *.ModuleB.Tests.dll;\n      *.ModuleC.Tests.dll";
            var matches = TestModulesFilterHandler.GetMatchedModulePaths(expression, root);

            matches.Select(Path.GetFileName)
                .Should().BeEquivalentTo("Some.ModuleA.Tests.dll", "Some.ModuleB.Tests.dll", "Some.ModuleC.Tests.dll");
        }

        [Fact]
        public void GetMatchedModulePaths_ExclusionPatternWithBang_ExcludesMatchingFiles()
        {
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_ExclusionPatternWithBang_ExcludesMatchingFiles)).Path;
            File.WriteAllText(Path.Combine(root, "Foo.mstest.exe"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Bar.mstest.exe"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Bar.toexclude.mstest.exe"), string.Empty);

            var matches = TestModulesFilterHandler.GetMatchedModulePaths("*.mstest.exe;!*.toexclude.mstest.exe", root);

            matches.Select(Path.GetFileName)
                .Should().BeEquivalentTo("Foo.mstest.exe", "Bar.mstest.exe");
        }

        [Fact]
        public void GetMatchedModulePaths_ExclusionPatternWithWhitespaceAfterBang_StillExcludes()
        {
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_ExclusionPatternWithWhitespaceAfterBang_StillExcludes)).Path;
            File.WriteAllText(Path.Combine(root, "Keep.dll"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Drop.exclude.dll"), string.Empty);

            var matches = TestModulesFilterHandler.GetMatchedModulePaths("*.dll ; !  *.exclude.dll", root);

            matches.Select(Path.GetFileName)
                .Should().BeEquivalentTo("Keep.dll");
        }

        [Fact]
        public void GetMatchedModulePaths_OnlyExcludePattern_ReturnsNothing()
        {
            // When there are no includes, the matcher has nothing to enumerate -> no results.
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_OnlyExcludePattern_ReturnsNothing)).Path;
            File.WriteAllText(Path.Combine(root, "Foo.dll"), string.Empty);

            var matches = TestModulesFilterHandler.GetMatchedModulePaths("!*.exclude.dll", root);

            matches.Should().BeEmpty();
        }

        [Fact]
        public void GetMatchedModulePaths_EmptyOrWhitespaceOnlyPattern_IsSkipped()
        {
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_EmptyOrWhitespaceOnlyPattern_IsSkipped)).Path;
            File.WriteAllText(Path.Combine(root, "Foo.dll"), string.Empty);

            var matches = TestModulesFilterHandler.GetMatchedModulePaths(" ; *.dll ;  ;", root);

            matches.Select(Path.GetFileName)
                .Should().BeEquivalentTo("Foo.dll");
        }

        [Fact]
        public void GetMatchedModulePaths_OnlyBangNoPattern_IsIgnored()
        {
            var root = TestAssetsManager.CreateTestDirectory(identifier: nameof(GetMatchedModulePaths_OnlyBangNoPattern_IsIgnored)).Path;
            File.WriteAllText(Path.Combine(root, "Foo.dll"), string.Empty);

            var matches = TestModulesFilterHandler.GetMatchedModulePaths("*.dll;!", root);

            matches.Select(Path.GetFileName)
                .Should().BeEquivalentTo("Foo.dll");
        }
    }
}