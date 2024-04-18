// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
