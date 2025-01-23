// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

public class DotNetFormatTests : SdkTests
{
    private const string BaselineSubDir = nameof(DotNetFormatTests);
    private const string TestFileName = "FormatTest.cs";
    private const string UnformattedFileName = "FormatTestUnformatted.cs";
    private const string ExpectedFormattedFileName = "FormatTestFormatted.cs";

    public DotNetFormatTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Format an unformatted project and verify that the output matches the pre-computed solution.
    /// </Summary>
    // Disabled due to https://github.com/dotnet/roslyn/issues/76797
    // [Fact]
    public void FormatProject()
    {
        if (DotNetHelper.IsMonoRuntime)
        {
            // TODO: Temporarily disabled due to https://github.com/dotnet/sdk/issues/37774
            return;
        }

        string unformattedCsFilePath = BaselineHelper.GetBaselineFilePath(UnformattedFileName, BaselineSubDir);

        string projectDirectory = DotNetHelper.ExecuteNew("console", nameof(FormatProject), "C#");

        string projectFilePath = Path.Combine(projectDirectory, nameof(FormatProject) + ".csproj");
        string testFilePath = Path.Combine(projectDirectory, TestFileName);

        File.Copy(unformattedCsFilePath, testFilePath);

        DotNetHelper.ExecuteCmd($"format {projectFilePath}");

        string unexpectedFormatCsFilePath = BaselineHelper.GetBaselineFilePath(ExpectedFormattedFileName, BaselineSubDir);

        BaselineHelper.CompareFiles(unexpectedFormatCsFilePath, testFilePath, OutputHelper);
    }
}
