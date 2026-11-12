// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class WindowsElevationTests
{
    [TestMethod]
    public void DisplayElevatedProcessOutput_DisplaysFileContents()
    {
        string outputFilePath = Path.GetTempFileName();

        try
        {
            File.WriteAllText(outputFilePath, "Elevated process output");
            using var output = new ConsoleOutputCapture();

            WindowsElevation.DisplayElevatedProcessOutput(outputFilePath);

            output.GetOutput().Should().Be($"Elevated process output{Environment.NewLine}");
        }
        finally
        {
            File.Delete(outputFilePath);
        }
    }
}
