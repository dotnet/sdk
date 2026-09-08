// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    [TestClass]
    public class DotDefaultPathCorrectorTests
    {
        [TestMethod]
        public void ItCanCorrectDotDefaultPath()
        {
            var existingPath = @"C:\Users\myname\AppData\Local\Microsoft\WindowsApps;C:\Users\myname\.dotnet\tools";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string correctPath).Should().BeTrue();
            correctPath.Should().Be(@"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps");
        }

        [TestMethod]
        public void ItCanTellNoCorrectionNeeded()
        {
            var existingPath = @"C:\Users\myname\AppData\Local\Microsoft\WindowsApps;";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string _).Should().BeFalse();
        }

        [TestMethod]
        public void GivenEmptyItCanTellNoCorrectionNeeded()
        {
            var existingPath = "";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string _).Should().BeFalse();
        }

        [TestMethod]
        public void GivenSubsequencePathItCanCorrectDotDefaultPath()
        {
            var existingPath =
                @"C:\Users\myname\AppData\Local\Microsoft\WindowsApps;C:\Users\myname\.dotnet\tools;C:\Users\myname\other;";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string correctPath).Should().BeTrue();
            correctPath.Should().Be(@"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;%USERPROFILE%\other");
        }

        [TestMethod]
        public void GivenSubsequencePathWithExtraFormatItCanCorrectDotDefaultPath()
        {
            var existingPath =
                @";C:\Users\myname\AppData\Local\Microsoft\WindowsApps;C:\Users\myname\.dotnet\tools;C:\Users\myname\other;;";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string correctPath).Should().BeTrue();
            correctPath.Should().Be(@"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;%USERPROFILE%\other");
        }

        [TestMethod]
        public void GivenNoToolPathItCanTellNoCorrectionNeeded()
        {
            var existingPath = @"C:\Users\myname\AppData\Local\Microsoft\WindowsApps;C:\Users\myname\other";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string _).Should().BeFalse();
        }

        [TestMethod]
        public void Given2InstallationItCanCorrectDotDefaultPath()
        {
            var existingPath = @"C:\Users\user1\AppData\Local\Microsoft\WindowsApps;C:\Users\user2\AppData\otherapp;C:\Users\user1\.dotnet\tools;C:\Users\user2\.dotnet\tools";
            DotDefaultPathCorrector.NeedCorrection(existingPath, out string correctPath).Should().BeTrue();
            correctPath.Should().Be(@"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;%USERPROFILE%\AppData\otherapp");
        }
    }
}
