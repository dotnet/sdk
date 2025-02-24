// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Publish.Tasks;


namespace Microsoft.Net.Sdk.Publish.Tasks.Tests
{
    public class WebJobsCommandGeneratorTests
    {
        [Theory]
        // Windows
        [InlineData("c:/test/WebApplication1.dll", false, ".exe", "dotnet WebApplication1.dll %*")]

        [InlineData("c:/test/WebApplication1.dll", true, ".exe", "WebApplication1.exe %*")]
        [InlineData("c:/test/WebApplication1.dll", true, "", "WebApplication1 %*")]

        [InlineData("c:/test/WebApplication1.exe", true, ".exe", "WebApplication1.exe %*")]
        [InlineData("c:/test/WebApplication1.exe", false, ".exe", "WebApplication1.exe %*")]

        [InlineData("/usr/test/WebApplication1.dll", true, ".sh", "WebApplication1.sh %*")]
        [InlineData("/usr/test/WebApplication1.dll", false, ".sh", "dotnet WebApplication1.dll %*")]

        //Linux
        [InlineData("c:/test/WebApplication1.dll", false, "", "#!/bin/bash\ndotnet WebApplication1.dll \"$@\"", true)]
        [InlineData("c:/test/WebApplication1.dll", true, "", "#!/bin/bash\n. WebApplication1 \"$@\"", true)]

        [InlineData("/usr/test/WebApplication1.dll", false, ".sh", "#!/bin/bash\ndotnet WebApplication1.dll \"$@\"", true)]
        [InlineData("/usr/test/WebApplication1.dll", true, ".sh", "#!/bin/bash\n. WebApplication1.sh \"$@\"", true)]
        public void WebJobsCommandGenerator_Generates_Correct_RunCmd(string targetPath, bool useAppHost, string executableExtension, string expected, bool isLinux = false)
        {
            // Arrange

            // Test
            string generatedRunCommand = WebJobsCommandGenerator.RunCommand(targetPath, useAppHost, executableExtension, isLinux);

            // Assert
            Assert.Equal(expected, generatedRunCommand);
        }
    }
}
