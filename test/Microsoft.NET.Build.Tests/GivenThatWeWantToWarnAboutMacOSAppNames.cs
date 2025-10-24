// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
#pragma warning disable CS8602 // Dereference of a possibly null reference
    public class GivenThatWeWantToWarnAboutMacOSAppNames : SdkTest
    {
        public GivenThatWeWantToWarnAboutMacOSAppNames(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_warns_when_app_name_ends_with_App_on_macOS()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTest.App"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=osx-x64")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("NETSDK1234");
        }

        [Fact]
        public void It_warns_when_app_name_ends_with_Service_on_macOS()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTest.Service"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=osx-arm64")
                .Should()
                .Pass()
                .And.HaveStdOutContaining("NETSDK1234");
        }

        [Fact]
        public void It_does_not_warn_when_app_name_does_not_end_with_App_or_Service()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTestApplication"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=osx-x64")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("NETSDK1234");
        }

        [Fact]
        public void It_does_not_warn_when_not_targeting_macOS()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTest.App"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=win-x64")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("NETSDK1234");
        }

        [Fact]
        public void It_can_be_suppressed_with_CheckMacOSAppName_property()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTest.App"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=osx-x64", "/p:CheckMacOSAppName=false")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("NETSDK1234");
        }

        [Fact]
        public void It_can_be_suppressed_with_NoWarn()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTest.App"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=osx-x64", "/p:NoWarn=NETSDK1234", "--verbosity:minimal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("NETSDK1234");
        }

        [Fact]
        public void It_does_not_warn_for_library_projects()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace!;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "AssemblyName", "MyTest.App"));
                    propertyGroup.Add(new XElement(ns + "OutputType", "Library"));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/p:RuntimeIdentifier=osx-x64")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("NETSDK1234");
        }
    }
}
