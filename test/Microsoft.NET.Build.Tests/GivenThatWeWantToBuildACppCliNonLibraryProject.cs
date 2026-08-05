// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantToBuildACppCliNonLibraryProject : SdkTest
    {

<<<<<<< HEAD
        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/54145")]
=======
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/54145")]
        [FullMSBuildOnly]
>>>>>>> origin/main
        public void Given_an_exe_project_It_should_fail_with_error_message()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("NETCoreCppClApp")
                .WithSource();

            new BuildCommand(testAsset, "NETCoreCppCliTest.sln")
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppNonDynamicLibraryDotnetCore);
        }

<<<<<<< HEAD
        [FullMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/54145")]
=======
        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/54145")]
        [FullMSBuildOnly]
>>>>>>> origin/main
        public void Given_an_StaticLibrary_project_It_should_fail_with_error_message()
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("NETCoreCppClApp")
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (Path.GetExtension(projectPath) == ".vcxproj")
                    {
                        XNamespace ns = project.Root.Name.Namespace;

                        foreach (var configurationType in project.Root.Descendants(ns + "ConfigurationType"))
                        {
                            configurationType.Value = "StaticLibrary";
                        }
                    }
                });

            new BuildCommand(testAsset, "NETCoreCppCliTest.sln")
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppNonDynamicLibraryDotnetCore);
        }
    }
}
