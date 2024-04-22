// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.TestFramework;
using RestoreCommand = Microsoft.DotNet.Tools.Test.Utilities.RestoreCommand;
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;
using RunCommand = Microsoft.DotNet.Tools.Test.Utilities.RunCommand;
using WindowsOnlyFactAttribute = Microsoft.DotNet.Tools.Test.Utilities.WindowsOnlyFactAttribute;
using TestBase = Microsoft.DotNet.Tools.Test.Utilities.TestBase;
using static Microsoft.DotNet.Tools.Test.Utilities.TestCommandExtensions;

namespace EndToEnd
{
    public class GivenNetFrameworkSupportsNetStandard2 : TestBase
    {
        [WindowsOnlyFact]
        public void ANET461ProjectCanReferenceANETStandardProject()
        {
            var _testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "NETFrameworkReferenceNETStandard20")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(_testInstance.Root.FullName, "TestApp");

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            new RunCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .ExecuteWithCapturedOutput()
                    .Should().Pass()
                         .And.HaveStdOutContaining("This string came from the test library!");

        }
    }
}
