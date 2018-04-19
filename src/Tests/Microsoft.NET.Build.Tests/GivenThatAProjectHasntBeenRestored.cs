﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatAProjectHasntBeenRestored : SdkTest
    {
        public GivenThatAProjectHasntBeenRestored(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("TestLibrary")]
        [InlineData("TestApp")]
        public void The_build_fails_if_nuget_restore_has_not_occurred(string relativeProjectPath)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            var projectDirectory = Path.Combine(testAsset.TestRoot, relativeProjectPath);

            VerifyNotRestoredFailure(projectDirectory);
        }

        private void VerifyNotRestoredFailure(string projectDirectory)
        {
            var buildCommand = new BuildCommand(Log, projectDirectory);

            var expectedError = Strings.AssetsFileNotSet;

            buildCommand
                //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
                .Execute("/clp:summary")
                .Should()
                .Fail()
                .And.HaveStdOutContaining(expectedError)
                //  We should only get one error
                .And.HaveStdOutContaining("1 Error(s)");
        }

        [Theory]
        [InlineData("TestLibrary")]
        [InlineData("TestApp")]
        public void The_design_time_build_succeeds_before_nuget_restore(string relativeProjectPath)
        {
            //  This test needs the design-time targets, which come with Visual Studio.  So we will use the VSINSTALLDIR
            //  environment variable to find the install path to Visual Studio and the design-time targets under it.
            //  This will be set when running from a developer command prompt.  Unfortunately, unless VS is launched
            //  from a developer command prompt, it won't be set when running tests from VS.  So in that case the
            //  test will simply be skipped.
            string vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");

            if (vsInstallDir == null)
            {
                return;
            }

            string csharpDesignTimeTargets = Path.Combine(vsInstallDir, @"MSBuild\Microsoft\VisualStudio\Managed\Microsoft.CSharp.DesignTime.targets");

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            var projectDirectory = Path.Combine(testAsset.TestRoot, relativeProjectPath);

            var args = new[]
            {
                "/p:DesignTimeBuild=true",
                "/p:SkipCompilerExecution=true",
                "/p:ProvideCommandLineArgs=true",
                $"/p:CSharpDesignTimeTargetsPath={csharpDesignTimeTargets}",
                "/t:ResolveProjectReferencesDesignTime",
                "/t:ResolveComReferencesDesignTime",
                "/t:CompileDesignTime",
                "/t:ResolvePackageDependenciesDesignTime"
            };

            var command = new MSBuildCommand(Log, "ResolveAssemblyReferencesDesignTime", projectDirectory);
            var result = command.Execute(args);

            result.Should().Pass();
        }
    }
}
