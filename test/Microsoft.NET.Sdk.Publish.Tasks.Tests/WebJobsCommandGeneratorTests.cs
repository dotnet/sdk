// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Publish.Tasks;


namespace Microsoft.Net.Sdk.Publish.Tasks.Tests
{
    [TestClass]
    public class WebJobsCommandGeneratorTests
    {
        [TestMethod]
        // Windows
        [DataRow("c:/test/WebApplication1.dll", false, ".exe", "dotnet WebApplication1.dll %*")]

        [DataRow("c:/test/WebApplication1.dll", true, ".exe", "WebApplication1.exe %*")]
        [DataRow("c:/test/WebApplication1.dll", true, "", "WebApplication1 %*")]

        [DataRow("c:/test/WebApplication1.exe", true, ".exe", "WebApplication1.exe %*")]
        [DataRow("c:/test/WebApplication1.exe", false, ".exe", "WebApplication1.exe %*")]

        [DataRow("/usr/test/WebApplication1.dll", true, ".sh", "WebApplication1.sh %*")]
        [DataRow("/usr/test/WebApplication1.dll", false, ".sh", "dotnet WebApplication1.dll %*")]

        //Linux
        [DataRow("c:/test/WebApplication1.dll", false, "", "#!/bin/bash\ndotnet WebApplication1.dll \"$@\"", true)]
        [DataRow("c:/test/WebApplication1.dll", true, "", "#!/bin/bash\n. WebApplication1 \"$@\"", true)]

        [DataRow("/usr/test/WebApplication1.dll", false, ".sh", "#!/bin/bash\ndotnet WebApplication1.dll \"$@\"", true)]
        [DataRow("/usr/test/WebApplication1.dll", true, ".sh", "#!/bin/bash\n. WebApplication1.sh \"$@\"", true)]
        public void WebJobsCommandGenerator_Generates_Correct_RunCmd(string targetPath, bool useAppHost, string executableExtension, string expected, bool isLinux = false)
        {
            // Arrange

            // Test
            string generatedRunCommand = WebJobsCommandGenerator.RunCommand(targetPath, useAppHost, executableExtension, isLinux);

            // Assert
            Assert.AreEqual(expected, generatedRunCommand);
        }
    }
}
