// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class DotNetFormatTests : SdkTests
{
    private const string TestFileName = "FormatTest.cs";
    private const string UnformattedFileName = "FormatTestUnformatted.cs";
    private const string ExpectedFormattedFileName = "FormatTestFormatted.cs";

    public DotNetFormatTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Format an unformatted project and verify that the output matches the pre-computed solution.
    /// </Summary>
    // https://github.com/dotnet/source-build/issues/3668
    // [Fact]
    public void FormatProject()
    {
        string unformattedCsFilePath = Path.Combine(BaselineHelper.GetAssetsDirectory(), UnformattedFileName);

        string projectDirectory = DotNetHelper.ExecuteNew("console", nameof(FormatProject), "C#");

        string projectFilePath = Path.Combine(projectDirectory, nameof(FormatProject) + ".csproj");
        string testFilePath = Path.Combine(projectDirectory, TestFileName);

        File.Copy(unformattedCsFilePath, testFilePath);

        DotNetHelper.ExecuteCmd($"format {projectFilePath}");

        BaselineHelper.CompareFiles(BaselineHelper.GetBaselineFilePath(ExpectedFormattedFileName), testFilePath, OutputHelper);
    }
}
