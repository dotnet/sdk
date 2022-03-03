// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class DotNetFormatTests : SmokeTests
{
    private const string UnformattedFileName = "FormatTestUnformatted.cs";
    private const string ExpectedFormattedFileName = "FormatTestExpectedFormatted.cs";

    public DotNetFormatTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Format an unformatted project and verify that the output matches the pre-computed solution.
    /// </Summary>
    [Fact]
    public void FormatProject()
    {
        string assetsDirectory = BaselineHelper.GetAssetsDirectory();

        string unformattedCsFilePath = Path.Combine(assetsDirectory, UnformattedFileName);
        string expectedFormattedCsFilePath = Path.Combine(assetsDirectory, ExpectedFormattedFileName);

        string projectDirectory = DotNetHelper.ExecuteNew("console", nameof(FormatProject), "C#");

        string projectFilePath = Path.Combine(projectDirectory, nameof(FormatProject) + ".csproj");
        string formattedCsFilePath = Path.Combine(projectDirectory, UnformattedFileName);

        File.Copy(unformattedCsFilePath, formattedCsFilePath);

        DotNetHelper.ExecuteCmd($"format {projectFilePath}");

        BaselineHelper.CompareFiles(expectedFormattedCsFilePath, formattedCsFilePath, OutputHelper);
    }
}
