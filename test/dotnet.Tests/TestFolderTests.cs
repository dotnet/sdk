// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Tests;

public class TestFolderTests(ITestOutputHelper log) : SdkTest(log)
{
    private const string FailureMessage = """
        the test directory was moved from 'src/Tests' to 'test' in main.
        Please copy/paste the contents in 'src/Tests' into 'test', delete the 'src/Tests' folder, and commit
        """;

    [Fact]
    public void GivenNoSrcTestsFolder()
    {
        // https://stackoverflow.com/a/47841442/294804
        static string GetThisFolderPath([CallerFilePath] string filePath = null) => Path.GetDirectoryName(filePath);

        var srcTestsFolderPath = Path.Combine(GetThisFolderPath(), @"..\..\src\Tests");
        Directory.Exists(srcTestsFolderPath).Should().BeFalse(FailureMessage);
    }
}

