// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenACheckForUnsupportedWinMDReferences
    {
        [Fact]
        public void NoReferences_Succeeds()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = Array.Empty<ITaskItem>(),
            };

            task.Execute().Should().BeTrue();
        }

        [Fact]
        public void NullReferencePaths_Throws()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = null,
            };

            Action act = () => task.Execute();
            act.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public void EmptyItemSpec_HandlesGracefully()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem("", new Dictionary<string, string>()),
                },
            };

            // Path.GetExtension("") returns "" which does not match ".winmd"
            task.Execute().Should().BeTrue("empty ItemSpec has no .winmd extension");
        }

        [Fact]
        public void WinMDExtensionOnly_LogsError()
        {
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem(".winmd", new Dictionary<string, string>()),
                },
            };

            // Path.GetExtension(".winmd") returns ".winmd", triggering the WinMD check path.
            task.Execute().Should().BeFalse("a bare .winmd reference triggers an error");
        }

        [Fact]
        public void RelativePathItemSpec_HandlesCorrectly()
        {
            const string relativeWinmdPath = "subfolder/something.winmd";
            var task = new CheckForUnsupportedWinMDReferences
            {
                BuildEngine = new MockBuildEngine(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v5.0",
                ReferencePaths = new ITaskItem[]
                {
                    new MockTaskItem(relativeWinmdPath, new Dictionary<string, string>()),
                },
            };

            task.Execute().Should().BeFalse("a .winmd reference triggers an error log, causing Execute to return false");
            var engine = (MockBuildEngine)task.BuildEngine;
            engine.Errors.Should().NotBeEmpty("the task should log an error for the unsupported .winmd reference");
        }
    }
}
