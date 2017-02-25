// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildALibraryWithTfm : SdkTest
    {
        [Fact]
        public void It_builds_the_monoandroid_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "monoandroid";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().HaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb"
            });
        }

        [Fact]
        public void It_builds_the_net40_client_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "net40-client";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_net45_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "net45";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_netstandard15_library_successfully()
        {
            const string tfm = "netstandard1.5";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.deps.json"
            });
        }

        [Fact]
        public void It_builds_the_portable_profile44_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "portable-Profile44";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("portable-win81+wpa81");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.pri",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_portable_profile151_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "portable-Profile151";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("portable-net451+wpa81+win81");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_portable_profile259_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "portable-Profile259";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("portable-net45+win8+wp8+wpa81");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_sl5_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "sl5";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_win8_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "win8";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.pri",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_win81_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "win81";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.pri",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_wp8_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "wp8";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_wp81_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "wp81";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_wpa81_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "wpa81";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.pri",
                $"Newtonsoft.Json.dll"
            });
        }

        [Fact]
        public void It_builds_the_uap10_0_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "uap10.0";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.pri"
            });
        }

        [Fact]
        public void It_builds_the_xamarinios_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "xamarinios";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().HaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.dll.mdb"
            });
        }

        [Fact]
        public void It_builds_the_xamarinmac_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "xamarinmac";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().HaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb"
            });
        }

        [Fact]
        public void It_builds_the_xamarintvos_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "xamarintvos";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().HaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.dll.mdb"
            });
        }

        [Fact]
        public void It_builds_the_xamarinwatchos_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            const string tfm = "xamarinwatchos";

            var testAsset = _testAssetsManager
                .CopyTestAsset("LibraryWithTfm")
                .WithSource()
                .Restore(tfm);

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, tfm);

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(tfm);

            outputDirectory.Should().HaveFiles(new[] {
                $"{tfm}.dll",
                $"{tfm}.pdb",
                $"{tfm}.dll.mdb"
            });
        }
    }
}